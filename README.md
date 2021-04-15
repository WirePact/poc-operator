# dotnet-solution-template

Template repository for dotnet solutions.

Contains solution-file with preconfigured build structure and
test folders.

Also, template files for releasing with semantic release are present
int the build folder.

mechanic:
- whenever a service with the annotation "ch.wirepact/for-deployment: Name Of Deployment"
  is around, the operator intercepts the creation and mutates the service with a specific
  port. So that the target of the call will be a specific injected port in the deployment.
- whenever a deployment with the annotation "ch.wirepact/port: PORT" is intercepted,
  the operator injects the sidecar with an envoy proxy and configures the upstream to
  the original application with "PORT"
