using Aspire.Hosting.Azure;
using Azure.Provisioning;
using Azure.Provisioning.Authorization;
using Azure.Provisioning.Expressions;
using Azure.Provisioning.Resources;
using Azure.Provisioning.Roles;
using Azure.Provisioning.Storage;

var builder = DistributedApplication.CreateBuilder(args);

var roles = builder.AddAzureInfrastructure("ids", infra =>
{
    var apiServiceId = new UserAssignedIdentity("apiServiceId");
    infra.Add(apiServiceId);

    var frontendServiceId = new UserAssignedIdentity("frontendServiceId");
    infra.Add(frontendServiceId);

    infra.Add(new ProvisioningOutput("apiServiceId", typeof(string)) { Value = apiServiceId.Id });
    infra.Add(new ProvisioningOutput("apiServicePrincipalId", typeof(string)) { Value = apiServiceId.PrincipalId });
    infra.Add(new ProvisioningOutput("apiServiceClientId", typeof(string)) { Value = apiServiceId.ClientId });

    infra.Add(new ProvisioningOutput("frontendServiceId", typeof(string)) { Value = frontendServiceId.Id });
    infra.Add(new ProvisioningOutput("frontendServicePrincipalId", typeof(string)) { Value = frontendServiceId.PrincipalId });
    infra.Add(new ProvisioningOutput("frontendServiceClientId", typeof(string)) { Value = frontendServiceId.ClientId });
});

var apiServiceId = new BicepOutputReference("apiServiceId", roles.Resource);
var apiServicePrincipalId = new BicepOutputReference("apiServicePrincipalId", roles.Resource);
var apiServiceClientId = new BicepOutputReference("apiServiceClientId", roles.Resource);

var frontendServiceId = new BicepOutputReference("frontendServiceId", roles.Resource);
var frontendServicePrincipalId = new BicepOutputReference("frontendServicePrincipalId", roles.Resource);
var frontendServiceClientId = new BicepOutputReference("frontendServiceClientId", roles.Resource);

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
        infra.Add(storageAccount.CreateRoleAssignment(StorageBuiltInRole.StorageBlobDataContributor, RoleManagementPrincipalType.ServicePrincipal, frontendServicePrincipalId.AsProvisioningParameter(infra)));
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

builder.AddProject<Projects.RoleExample_Web>("webfrontend")
    .PublishAsAzureContainerApp((infra, app) =>
    {
        var frontendServiceIdParam = frontendServiceId.AsProvisioningParameter(infra);
        var id = BicepFunction.Interpolate($"{frontendServiceIdParam}").Compile().ToString();

        app.Identity.UserAssignedIdentities[id] = new UserAssignedIdentityDetails();

        var clientIdEnv = app.Template.Containers[0].Value!.Env.Single(e => e.Value!.Name.Value == "AZURE_CLIENT_ID");
        clientIdEnv.Value!.Value = frontendServiceClientId.AsProvisioningParameter(infra);
    })
    .WithExternalHttpEndpoints()
    .WithReference(blobs)
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
