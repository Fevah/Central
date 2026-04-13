namespace TIG.IntegrationServer.Plugin.Core.ServiceContext.AuthenticationServiceContext.Contract
{
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ServiceModel.ServiceContractAttribute(ConfigurationName="IAuthenticationMethodService")]
    public interface IAuthenticationMethodService
    {
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IMethodServiceBase/Ping", ReplyAction="http://tempuri.org/IMethodServiceBase/PingResponse")]
        void Ping();
    
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IMethodServiceBase/Ping", ReplyAction="http://tempuri.org/IMethodServiceBase/PingResponse")]
        System.Threading.Tasks.Task PingAsync();
    
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IAuthenticationMethodService/Login", ReplyAction="http://tempuri.org/IAuthenticationMethodService/LoginResponse")]
        [System.ServiceModel.FaultContractAttribute(typeof(ServiceFault), Action="http://tempuri.org/IAuthenticationMethodService/LoginServiceFaultFault", Name="ServiceFault", Namespace="http://schemas.datacontract.org/2004/07/TIG.TotalLink.Shared.Contract.Core")]
        string Login(string userName, string password);
    
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IAuthenticationMethodService/Login", ReplyAction="http://tempuri.org/IAuthenticationMethodService/LoginResponse")]
        System.Threading.Tasks.Task<string> LoginAsync(string userName, string password);
    
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IAuthenticationMethodService/LoginByActiveDirectory", ReplyAction="http://tempuri.org/IAuthenticationMethodService/LoginByActiveDirectoryResponse")]
        [System.ServiceModel.FaultContractAttribute(typeof(ServiceFault), Action="http://tempuri.org/IAuthenticationMethodService/LoginByActiveDirectoryServiceFaul" +
                                                                                 "tFault", Name="ServiceFault", Namespace="http://schemas.datacontract.org/2004/07/TIG.TotalLink.Shared.Contract.Core")]
        string LoginByActiveDirectory(byte[] userSid, System.Guid userGuid);
    
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IAuthenticationMethodService/LoginByActiveDirectory", ReplyAction="http://tempuri.org/IAuthenticationMethodService/LoginByActiveDirectoryResponse")]
        System.Threading.Tasks.Task<string> LoginByActiveDirectoryAsync(byte[] userSid, System.Guid userGuid);
    }
}