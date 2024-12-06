using Aspire.Hosting.Azure;
using Azure.Provisioning;
using Azure.Provisioning.Authorization;
using Azure.Provisioning.Expressions;
using Azure.Provisioning.Resources;
using Azure.Provisioning.Roles;
using Azure.Provisioning.Storage;

var builder = DistributedApplication.CreateBuilder(args);

var ids = builder.AddAzureInfrastructure("ids", infra =>
{
    var apiServiceId = new UserAssignedIdentity("apiServiceId");
    infra.Add(apiServiceId);

    var webId = new UserAssignedIdentity("webId");
    infra.Add(webId);

    infra.Add(new ProvisioningOutput("apiServiceId", typeof(string)) { Value = apiServiceId.Id });
    infra.Add(new ProvisioningOutput("apiServicePrincipalId", typeof(string)) { Value = apiServiceId.PrincipalId });
    infra.Add(new ProvisioningOutput("apiServiceClientId", typeof(string)) { Value = apiServiceId.ClientId });

    infra.Add(new ProvisioningOutput("webId", typeof(string)) { Value = webId.Id });
    infra.Add(new ProvisioningOutput("webPrincipalId", typeof(string)) { Value = webId.PrincipalId });
    infra.Add(new ProvisioningOutput("webClientId", typeof(string)) { Value = webId.ClientId });
});

var apiServiceId = new BicepOutputReference("apiServiceId", ids.Resource);
var apiServicePrincipalId = new BicepOutputReference("apiServicePrincipalId", ids.Resource);
var apiServiceClientId = new BicepOutputReference("apiServiceClientId", ids.Resource);

var webId = new BicepOutputReference("webId", ids.Resource);
var webPrincipalId = new BicepOutputReference("webPrincipalId", ids.Resource);
var webClientId = new BicepOutputReference("webClientId", ids.Resource);

var blobs = builder.AddAzureStorage("storage")
    .RunAsEmulator(e => e.WithLifetime(ContainerLifetime.Persistent))
    .ConfigureInfrastructure(infra =>
    {
        var roles = infra.GetProvisionableResources().OfType<RoleAssignment>().ToList();

        foreach (var role in roles)
        {
            infra.Remove(role);
        }

        var storageAccount = infra.GetProvisionableResources().OfType<StorageAccount>().Single();
        infra.Add(storageAccount.CreateRoleAssignment(StorageBuiltInRole.StorageBlobDataContributor, RoleManagementPrincipalType.ServicePrincipal, webPrincipalId.AsProvisioningParameter(infra)));
        infra.Add(storageAccount.CreateRoleAssignment(StorageBuiltInRole.StorageBlobDataReader, RoleManagementPrincipalType.ServicePrincipal, apiServicePrincipalId.AsProvisioningParameter(infra)));
    })
    .AddBlobs("blobs");

var apiService = builder.AddProject<Projects.RoleExample_ApiService>("apiservice")
    .WithReference(blobs)
    .PublishAsAzureContainerApp((infra, app) =>
    {
        var apiServiceIdParam = apiServiceId.AsProvisioningParameter(infra);
        var id = BicepFunction.Interpolate($"{apiServiceIdParam}").Compile().ToString();

        app.Identity.UserAssignedIdentities[id] = new UserAssignedIdentityDetails();

        var clientIdEnv = app.Template.Containers[0].Value!.Env.Single(e => e.Value!.Name.Value == "AZURE_CLIENT_ID");
        clientIdEnv.Value!.Value = apiServiceClientId.AsProvisioningParameter(infra);
    });

builder.AddProject<Projects.RoleExample_Web>("web")
    .PublishAsAzureContainerApp((infra, app) =>
    {
        var webIdParam = webId.AsProvisioningParameter(infra);
        var id = BicepFunction.Interpolate($"{webIdParam}").Compile().ToString();

        app.Identity.UserAssignedIdentities[id] = new UserAssignedIdentityDetails();

        var clientIdEnv = app.Template.Containers[0].Value!.Env.Single(e => e.Value!.Name.Value == "AZURE_CLIENT_ID");
        clientIdEnv.Value!.Value = webClientId.AsProvisioningParameter(infra);
    })
    .WithExternalHttpEndpoints()
    .WithReference(blobs)
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
