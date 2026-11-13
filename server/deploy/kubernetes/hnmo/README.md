# HNMO Helm chart

*Experimental.* Deploys the distributed deployment of HNMO on Kubernetes, with Dapr.

See the [Kubernetes documentation](../../../docs-website/docs/deployment/kubernetes.md) for the
requirements (Dapr, the images, PostgreSQL, RabbitMQ and Redis), the installation and the
limitations, and [`values.yaml`](values.yaml) for all settings.

```bash
helm install hnmo . --namespace hnmo --create-namespace \
  --set image.registry=<registry> --set image.tag=<tag>
```
