<?xml version="1.0" encoding="utf-8"?>
<configurationSectionModel xmlns:dm0="http://schemas.microsoft.com/VisualStudio/2008/DslTools/Core" dslVersion="1.0.0.0" Id="62a50d99-53a2-4c41-8e92-ad3c55d28490" namespace="TIG.IntegrationServer.Common.Configuration" xmlSchemaNamespace="urn:TIG.IntegrationServer.Common.Configuration" xmlns="http://schemas.microsoft.com/dsltools/ConfigurationSectionDesigner">
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
    <configurationSection name="IntegrationServiceSection" codeGenOptions="Singleton, XmlnsProperty" xmlSectionName="integrationServiceSection">
      <elementProperties>
        <elementProperty name="ConcurrentSettings" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="concurrentSettings" isReadOnly="false">
          <type>
            <configurationElementMoniker name="/62a50d99-53a2-4c41-8e92-ad3c55d28490/ConcurrentSettings" />
          </type>
        </elementProperty>
        <elementProperty name="SyncTaskSettings" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="syncTaskSettings" isReadOnly="false">
          <type>
            <configurationElementMoniker name="/62a50d99-53a2-4c41-8e92-ad3c55d28490/SyncTaskSettings" />
          </type>
        </elementProperty>
        <elementProperty name="IntegrationServiceSettings" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="integrationServiceSettings" isReadOnly="false">
          <type>
            <configurationElementMoniker name="/62a50d99-53a2-4c41-8e92-ad3c55d28490/IntegrationServiceSettings" />
          </type>
        </elementProperty>
      </elementProperties>
    </configurationSection>
    <configurationElement name="ConcurrentSettings">
      <attributeProperties>
        <attributeProperty name="SyncTasksLimit" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="syncTasksLimit" isReadOnly="false">
          <type>
            <externalTypeMoniker name="/62a50d99-53a2-4c41-8e92-ad3c55d28490/Int32" />
          </type>
        </attributeProperty>
        <attributeProperty name="SyncInstanceBundleTasksPerSyncEntityBundleTaskLimit" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="syncInstanceBundleTasksPerSyncEntityBundleTaskLimit" isReadOnly="false">
          <type>
            <externalTypeMoniker name="/62a50d99-53a2-4c41-8e92-ad3c55d28490/Int32" />
          </type>
        </attributeProperty>
        <attributeProperty name="SyncInstanceTasksPerSyncInstanceBundleTaskLimit" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="syncInstanceTasksPerSyncInstanceBundleTaskLimit" isReadOnly="false">
          <type>
            <externalTypeMoniker name="/62a50d99-53a2-4c41-8e92-ad3c55d28490/Int32" />
          </type>
        </attributeProperty>
      </attributeProperties>
    </configurationElement>
    <configurationElement name="SyncTaskSettings">
      <attributeProperties>
        <attributeProperty name="ActiveSyncTasksPollingTimeout" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="activeSyncTasksPollingTimeout" isReadOnly="false">
          <type>
            <externalTypeMoniker name="/62a50d99-53a2-4c41-8e92-ad3c55d28490/Int32" />
          </type>
        </attributeProperty>
        <attributeProperty name="DefaultSyncTaskTimeout" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="defaultSyncTaskTimeout" isReadOnly="false">
          <type>
            <externalTypeMoniker name="/62a50d99-53a2-4c41-8e92-ad3c55d28490/Int32" />
          </type>
        </attributeProperty>
      </attributeProperties>
    </configurationElement>
    <configurationElement name="IntegrationServiceSettings">
      <elementProperties>
        <elementProperty name="AuthenticationSettings" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="authenticationSettings" isReadOnly="false">
          <type>
            <configurationElementMoniker name="/62a50d99-53a2-4c41-8e92-ad3c55d28490/AuthenticationSettings" />
          </type>
        </elementProperty>
      </elementProperties>
    </configurationElement>
    <configurationElement name="AuthenticationSettings">
      <attributeProperties>
        <attributeProperty name="LoginName" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="loginName" isReadOnly="false">
          <type>
            <externalTypeMoniker name="/62a50d99-53a2-4c41-8e92-ad3c55d28490/String" />
          </type>
        </attributeProperty>
        <attributeProperty name="Password" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="password" isReadOnly="false">
          <type>
            <externalTypeMoniker name="/62a50d99-53a2-4c41-8e92-ad3c55d28490/String" />
          </type>
        </attributeProperty>
        <attributeProperty name="Method" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="method" isReadOnly="false">
          <type>
            <externalTypeMoniker name="/62a50d99-53a2-4c41-8e92-ad3c55d28490/String" />
          </type>
        </attributeProperty>
      </attributeProperties>
    </configurationElement>
  </configurationElements>
  <propertyValidators>
    <validators />
  </propertyValidators>
</configurationSectionModel>