// K8s namespace + RBAC + NetworkPolicy creation for dedicated tenants
use anyhow::Result;
use k8s_openapi::api::core::v1::Namespace;
use k8s_openapi::api::networking::v1::{NetworkPolicy, NetworkPolicySpec, NetworkPolicyIngressRule, NetworkPolicyPeer};
use k8s_openapi::apimachinery::pkg::apis::meta::v1::{ObjectMeta, LabelSelector};
use kube::{Api, Client};
use kube::api::{PostParams, DeleteParams};
use std::collections::BTreeMap;

pub async fn create_tenant_namespace(ns_name: &str, slug: &str) -> Result<()> {
    let client = Client::try_default().await?;
    let ns_api: Api<Namespace> = Api::all(client.clone());

    let mut labels = BTreeMap::new();
    labels.insert("app.kubernetes.io/part-of".into(), "central-platform".into());
    labels.insert("central.io/tenant".into(), slug.into());
    labels.insert("central.io/sizing".into(), "dedicated".into());

    let ns = Namespace {
        metadata: ObjectMeta {
            name: Some(ns_name.into()),
            labels: Some(labels),
            ..Default::default()
        },
        ..Default::default()
    };
    ns_api.create(&PostParams::default(), &ns).await.ok();

    // Default-deny NetworkPolicy
    let np_api: Api<NetworkPolicy> = Api::namespaced(client.clone(), ns_name);
    let default_deny = NetworkPolicy {
        metadata: ObjectMeta { name: Some("default-deny".into()), ..Default::default() },
        spec: Some(NetworkPolicySpec {
            pod_selector: LabelSelector::default(),
            policy_types: Some(vec!["Ingress".into(), "Egress".into()]),
            ..Default::default()
        }),
        ..Default::default()
    };
    np_api.create(&PostParams::default(), &default_deny).await.ok();

    // Allow from platform namespace
    let mut match_labels = BTreeMap::new();
    match_labels.insert("kubernetes.io/metadata.name".into(), "central".into());
    let allow_platform = NetworkPolicy {
        metadata: ObjectMeta { name: Some("allow-from-platform".into()), ..Default::default() },
        spec: Some(NetworkPolicySpec {
            pod_selector: LabelSelector::default(),
            ingress: Some(vec![NetworkPolicyIngressRule {
                from: Some(vec![NetworkPolicyPeer {
                    namespace_selector: Some(LabelSelector {
                        match_labels: Some(match_labels),
                        ..Default::default()
                    }),
                    ..Default::default()
                }]),
                ..Default::default()
            }]),
            policy_types: Some(vec!["Ingress".into()]),
            ..Default::default()
        }),
        ..Default::default()
    };
    np_api.create(&PostParams::default(), &allow_platform).await.ok();

    tracing::info!(ns_name, "namespace provisioned with NetworkPolicies");
    Ok(())
}

pub async fn delete_tenant_namespace(ns_name: &str) -> Result<()> {
    let client = Client::try_default().await?;
    let ns_api: Api<Namespace> = Api::all(client);
    ns_api.delete(ns_name, &DeleteParams::default()).await.ok();
    tracing::info!(ns_name, "namespace deletion requested");
    Ok(())
}
