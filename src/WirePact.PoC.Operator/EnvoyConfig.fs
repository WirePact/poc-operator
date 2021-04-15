module WirePact.PoC.Operator.EnvoyConfig

let private template envoyPort targetPort translatorPort =
    $@"
static_resources:
  listeners:
    - name: listener_0
      address:
        socket_address:
          protocol: TCP
          address: 0.0.0.0
          port_value: {envoyPort}
      filter_chains:
        - filters:
            - name: envoy.filters.network.http_connection_manager
              typed_config:
                '@type': type.googleapis.com/envoy.extensions.filters.network.http_connection_manager.v3.HttpConnectionManager
                stat_prefix: ingress_http
                access_log:
                  - name: envoy.access_loggers.file
                    typed_config:
                      '@type': type.googleapis.com/envoy.extensions.access_loggers.file.v3.FileAccessLog
                      path: /dev/stdout
                route_config:
                  name: local_route
                  virtual_hosts:
                    - name: local_service
                      domains: ['*']
                      routes:
                        - match:
                            prefix: '/'
                          route:
                            cluster: target_service
                http_filters:
                  - name: envoy.filters.http.ext_authz
                    typed_config:
                      '@type': type.googleapis.com/envoy.extensions.filters.http.ext_authz.v3.ExtAuthz
                      transport_api_version: v3
                      grpc_service:
                        envoy_grpc:
                          cluster_name: auth_translator
                        # Default is 200ms; override if your server needs e.g. warmup time.
                        timeout: 1s
                      include_peer_certificate: true
                  - name: envoy.filters.http.router
  clusters:
    - name: target_service
      connect_timeout: 30s
      type: STATIC
      load_assignment:
        cluster_name: target_service
        endpoints:
          - lb_endpoints:
              - endpoint:
                  address:
                    socket_address:
                      address: 127.0.0.1
                      port_value: {targetPort}
    - name: auth_translator
      connect_timeout: 0.25s
      type: STATIC
      typed_extension_protocol_options:
        envoy.extensions.upstreams.http.v3.HttpProtocolOptions:
          '@type': type.googleapis.com/envoy.extensions.upstreams.http.v3.HttpProtocolOptions
          explicit_http_config:
            http2_protocol_options: {{}}
      load_assignment:
        cluster_name: auth_translator
        endpoints:
          - lb_endpoints:
              - endpoint:
                  address:
                    socket_address:
                      address: 127.0.0.1
                      port_value: {translatorPort}"
