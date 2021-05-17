using System;
using System.Linq;
using System.Threading.Tasks;
using DotnetKubernetesClient;
using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Rbac;
using Microsoft.Extensions.Logging;

namespace WirePact.PoC.Operator
{
    [EntityRbac(typeof(V1Service), Verbs = RbacVerb.All)]
    public class WirePactServiceController : IResourceController<V1Service>
    {
        private const string DeploymentAnnotation = "ch.wirepact/port";
        private const string ServiceAnnotation = "ch.wirepact/deployment";

        private readonly ILogger<WirePactDeploymentController> _logger;
        private readonly IKubernetesClient _client;

        public WirePactServiceController(ILogger<WirePactDeploymentController> logger, IKubernetesClient client)
        {
            _logger = logger;
            _client = client;
        }

        public Task<ResourceControllerResult?> CreatedAsync(V1Service entity) => CheckService(entity);

        public Task<ResourceControllerResult?> UpdatedAsync(V1Service entity) => CheckService(entity);

        public Task<ResourceControllerResult?> NotModifiedAsync(V1Service entity) => CheckService(entity);

        private async Task<ResourceControllerResult?> CheckService(V1Service entity)
        {
            if (entity.GetAnnotation(ServiceAnnotation) == null)
            {
                _logger.LogInformation($"Service {entity.Name()} has no wirepact annotation.");
                return null;
            }

            var targetPort = Convert.ToInt32(entity.GetAnnotation(DeploymentAnnotation));

            if (entity.Spec.Ports.Any(p => p.Port == targetPort && p.TargetPort == "envoy"))
            {
                _logger.LogInformation($"Service {entity.Name()} has envoy target set.");
                return null;
            }

            var port = entity.Spec.Ports.FirstOrDefault(p => p.Port == targetPort);
            if (port == null)
            {
                _logger.LogInformation($"Service {entity.Name()} has no port to the target.");
                return null;
            }

            port.TargetPort = "envoy";
            _logger.LogInformation($"Update Service {entity.Name()}.");
            await _client.Update(entity);

            return null;
        }
    }
}
