using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotnetKubernetesClient;
using k8s;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Entities.Extensions;
using KubeOps.Operator.Rbac;
using Microsoft.Extensions.Logging;

namespace WirePact.PoC.Operator
{
    [EntityRbac(typeof(V1Deployment), typeof(V1ConfigMap), Verbs = RbacVerb.All)]
    public class WirePactDeploymentController : IResourceController<V1Deployment>
    {
        private const string DeploymentAnnotation = "ch.wirepact/port";
        private const string CredentialsAnnotation = "ch.wirepact/credentials";
        private const string EnvoyAnnotation = "ch.wirepact/envoy-port";
        private const string TranslatorAnnotation = "ch.wirepact/translator-port";


        private const string EnvoyImage = "envoyproxy/envoy-alpine:v1.17-latest";
        private const string TranslatorImage = "ghcr.io/wirepact/translators/poc-demo-translator:latest";

        private const int LowerPort = 40000;
        private const int UpperPort = 60000;

        private static readonly Random Random = new();
        private readonly ILogger<WirePactDeploymentController> _logger;
        private readonly IKubernetesClient _client;

        public WirePactDeploymentController(ILogger<WirePactDeploymentController> logger, IKubernetesClient client)
        {
            _logger = logger;
            _client = client;
        }

        public Task<ResourceControllerResult?> CreatedAsync(V1Deployment entity) => CheckDeployment(entity);

        public Task<ResourceControllerResult?> UpdatedAsync(V1Deployment entity) => CheckDeployment(entity);

        public Task<ResourceControllerResult?> NotModifiedAsync(V1Deployment entity) => CheckDeployment(entity);

        private static int RandomPort(IReadOnlyList<int> usedPorts)
        {
            int newPort;
            do
            {
                newPort = Random.Next(LowerPort, UpperPort);
            }
            while (usedPorts.Contains(newPort));

            return newPort;
        }

        private async Task<ResourceControllerResult?> CheckDeployment(V1Deployment entity)
        {
            if (entity.GetAnnotation(DeploymentAnnotation) == null)
            {
                _logger.LogInformation($"Deployment {entity.Name()} has no wirepact annotation.");
                return null;
            }

            if (entity.GetAnnotation(EnvoyAnnotation) != null && entity.GetAnnotation(TranslatorAnnotation) != null)
            {
                _logger.LogInformation($"Deployment {entity.Name()} has everything set up.");
                return null;
            }

            var usedPorts = entity.Spec.Template.Spec.Containers
                .SelectMany(c => c.Ports)
                .Select(p => p.ContainerPort)
                .ToList();

            var envoyPort = RandomPort(usedPorts);
            usedPorts.Add(envoyPort);
            var translatorPort = RandomPort(usedPorts);
            usedPorts.Add(translatorPort);

            entity.SetAnnotation("ch.wirepact/envoy-port", envoyPort.ToString());
            entity.SetAnnotation("ch.wirepact/translator-port", translatorPort.ToString());

            _logger.LogInformation(
                $"Deployment {entity.Name()} contains wirepact annotation. " +
                $"Configure envoy-port: {envoyPort}; " +
                $"translator-port: {translatorPort}");

            var envoyConfig = new V1ConfigMap().Initialize();
            envoyConfig.SetAnnotation("ch.wirepact/deployment", entity.Name());
            envoyConfig.SetAnnotation("ch.wirepact/deployment-ns", entity.Namespace());
            envoyConfig.Metadata.Name = $"wp-envoy-{entity.Name()}";
            envoyConfig.Metadata.NamespaceProperty = entity.Namespace();
            envoyConfig.AddOwnerReference(entity.MakeOwnerReference());
            envoyConfig.Data = new Dictionary<string, string>
            {
                {
                    "envoy-config.yaml",
                    EnvoyConfig.Bootstrap(
                        envoyPort,
                        int.Parse(entity.GetAnnotation(DeploymentAnnotation)),
                        translatorPort)
                },
            };

            _logger.LogInformation($"Create configmap {envoyConfig.Name()}.");
            if (await _client.Get<V1ConfigMap>(envoyConfig.Name(), envoyConfig.Namespace()) != null)
            {
                _logger.LogDebug($"ConfigMap {envoyConfig.Name()} already existed. Delete.");
                await _client.Delete(envoyConfig);
            }

            await _client.Create(envoyConfig);

            _logger.LogDebug("Adding translator and envoy sidecars.");

            entity.Spec.Template.Spec.Volumes ??= new List<V1Volume>();
            entity.Spec.Template.Spec.Volumes.Add(
                new()
                {
                    Name = "envoy-config",
                    ConfigMap = new(name: envoyConfig.Name()),
                });

            entity.Spec.Template.Spec.Containers.Add(
                new()
                {
                    Name = "wirepact-envoy",
                    Image = EnvoyImage,
                    Ports = new List<V1ContainerPort>
                    {
                        new(envoyPort, name: "envoy"),
                    },
                    VolumeMounts = new List<V1VolumeMount>
                    {
                        new("/config/envoy.yaml", "envoy-config", readOnlyProperty: true, subPath: "envoy-config.yaml"),
                    },
                    Command = new[] { "envoy", "-c", "/config/envoy.yaml", "--component-log-level", "ext_authz:trace" },
                });

            entity.Spec.Template.Spec.Containers.Add(
                new()
                {
                    Name = "wirepact-translator",
                    Image = TranslatorImage,
                    Env = new List<V1EnvVar>
                    {
                        new("LOCAL_PORT", translatorPort.ToString()),
                        new("CREDENTIALS_SECRET_NAME", entity.GetAnnotation(CredentialsAnnotation)),
                    },
                });

            _logger.LogInformation($"Update Deployment {entity.Name()}.");
            await _client.Update(entity);

            return null;
        }
    }
}
