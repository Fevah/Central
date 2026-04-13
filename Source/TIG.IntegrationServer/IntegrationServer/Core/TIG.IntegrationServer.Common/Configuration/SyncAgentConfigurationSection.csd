<?xml version="1.0" encoding="utf-8"?>
<configurationSectionModel xmlns:dm0="http://schemas.microsoft.com/VisualStudio/2008/DslTools/Core" dslVersion="1.0.0.0" Id="57e9161a-0c7f-4546-9d16-e6833d4502dc" namespace="TIG.IntegrationServer.Common.Configuration" xmlSchemaNamespace="urn:TIG.IntegrationServer.Common.Configuration" xmlns="http://schemas.microsoft.com/dsltools/ConfigurationSectionDesigner">
  <typeDefinitions>
    <externalType name="String" namespace="System" />
    <externalType name="Boolean" namespace="System" />
    <externalType name="Int32" namespace="System" />
    <externalType name="Int64" namespace="System" />
    <externalType name="Single" namespace="System" />
    <externalType name="Double" namespace="System" />
    <externalType name="DateTime" namespace="System" />
    <externalType name="TimeSpan" namespace="System" />
  </typeDefinitions>
  <configurationElements>
    <configurationSection name="SyncAgentConfigurationSection" codeGenOptions="Singleton, XmlnsProperty" xmlSectionName="syncAgentConfigurationSection">
      <elementProperties>
        <elementProperty name="SyncServiceSettings" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="syncServiceSettings" isReadOnly="false">
          <type>
            <configurationElementMoniker name="/57e9161a-0c7f-4546-9d16-e6833d4502dc/SyncServiceSettings" />
          </type>
        </elementProperty>
        <elementProperty name="ChangeTrackerSettings" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="changeTrackerSettings" isReadOnly="false">
          <type>
            <configurationElementMoniker name="/57e9161a-0c7f-4546-9d16-e6833d4502dc/ChangeTrackerConfigurationElement" />
          </type>
        </elementProperty>
      </elementProperties>
    </configurationSection>
    <configurationElement name="AuthenticationConfigurationElement">
      <attributeProperties>
        <attributeProperty name="ServiceUri" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="serviceUri" isReadOnly="false">
          <type>
            <externalTypeMoniker name="/57e9161a-0c7f-4546-9d16-e6833d4502dc/String" />
          </type>
        </attributeProperty>
        <attributeProperty name="Login" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="login" isReadOnly="false">
          <type>
            <externalTypeMoniker name="/57e9161a-0c7f-4546-9d16-e6833d4502dc/String" />
          </type>
        </attributeProperty>
        <attributeProperty name="Password" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="password" isReadOnly="false">
          <type>
            <externalTypeMoniker name="/57e9161a-0c7f-4546-9d16-e6833d4502dc/String" />
          </type>
        </attributeProperty>
      </attributeProperties>
    </configurationElement>
    <configurationElement name="SyncServiceSettings">
      <attributeProperties>
        <attributeProperty name="ServiceUri" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="serviceUri" isReadOnly="false">
          <type>
            <externalTypeMoniker name="/57e9161a-0c7f-4546-9d16-e6833d4502dc/String" />
          </type>
        </attributeProperty>
        <attributeProperty name="Authentication" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="authentication" isReadOnly="false">
          <type>
            <externalTypeMoniker name="/57e9161a-0c7f-4546-9d16-e6833d4502dc/Boolean" />
          </type>
        </attributeProperty>
        <attributeProperty name="MetadataUri" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="metadataUri" isReadOnly="false">
          <type>
            <externalTypeMoniker name="/57e9161a-0c7f-4546-9d16-e6833d4502dc/String" />
          </type>
        </attributeProperty>
      </attributeProperties>
      <elementProperties>
        <elementProperty name="AuthenticationSettings" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="authenticationSettings" isReadOnly="false">
          <type>
            <configurationElementMoniker name="/57e9161a-0c7f-4546-9d16-e6833d4502dc/AuthenticationConfigurationElement" />
          </type>
        </elementProperty>
        <elementProperty name="NetworkCredential" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="networkCredential" isReadOnly="false">
          <type>
            <configurationElementMoniker name="/57e9161a-0c7f-4546-9d16-e6833d4502dc/NetworkCredential" />
          </type>
        </elementProperty>
      </elementProperties>
    </configurationElement>
    <configurationElement name="ChangeTrackerConfigurationElement">
      <attributeProperties>
        <attributeProperty name="Provider" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="provider" isReadOnly="false">
          <type>
            <externalTypeMoniker name="/57e9161a-0c7f-4546-9d16-e6833d4502dc/String" />
          </type>
        </attributeProperty>
        <attributeProperty name="ConnectionString" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="connectionString" isReadOnly="false">
          <type>
            <externalTypeMoniker name="/57e9161a-0c7f-4546-9d16-e6833d4502dc/String" />
          </type>
        </attributeProperty>
      </attributeProperties>
    </configurationElement>
    <configurationElement name="NetworkCredential">
      <attributeProperties>
        <attributeProperty name="Username" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="username" isReadOnly="false">
          <type>
            <externalTypeMoniker name="/57e9161a-0c7f-4546-9d16-e6833d4502dc/String" />
          </type>
        </attributeProperty>
        <attributeProperty name="Password" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="password" isReadOnly="false">
          <type>
            <externalTypeMoniker name="/57e9161a-0c7f-4546-9d16-e6833d4502dc/String" />
          </type>
        </attributeProperty>
        <attributeProperty name="Domain" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="domain" isReadOnly="false">
          <type>
            <externalTypeMoniker name="/57e9161a-0c7f-4546-9d16-e6833d4502dc/String" />
          </type>
        </attributeProperty>
      </attributeProperties>
    </configurationElement>
  </configurationElements>
  <propertyValidators>
    <validators />
  </propertyValidators>
</configurationSectionModel>