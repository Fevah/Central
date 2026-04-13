<?xml version="1.0" encoding="utf-8"?>
<configurationSectionModel xmlns:dm0="http://schemas.microsoft.com/VisualStudio/2008/DslTools/Core" dslVersion="1.0.0.0" Id="91145ccd-9a30-4f07-8f34-066fe5fb6cb8" namespace="TIG.TotalLink.Server.Core.Configuration" xmlSchemaNamespace="urn:TIG.TotalLink.Server.Core.Configuration" xmlns="http://schemas.microsoft.com/dsltools/ConfigurationSectionDesigner">
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
    <configurationSection name="ActiveDirectoryConfigurationSection" codeGenOptions="Singleton, XmlnsProperty" xmlSectionName="activeDirectoryConfigurationSection">
      <attributeProperties>
        <attributeProperty name="Root" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="root" isReadOnly="false">
          <type>
            <externalTypeMoniker name="/91145ccd-9a30-4f07-8f34-066fe5fb6cb8/String" />
          </type>
        </attributeProperty>
        <attributeProperty name="Domain" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="domain" isReadOnly="false">
          <type>
            <externalTypeMoniker name="/91145ccd-9a30-4f07-8f34-066fe5fb6cb8/String" />
          </type>
        </attributeProperty>
        <attributeProperty name="User" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="user" isReadOnly="false">
          <type>
            <externalTypeMoniker name="/91145ccd-9a30-4f07-8f34-066fe5fb6cb8/String" />
          </type>
        </attributeProperty>
        <attributeProperty name="Password" isRequired="false" isKey="false" isDefaultCollection="false" xmlName="password" isReadOnly="false">
          <type>
            <externalTypeMoniker name="/91145ccd-9a30-4f07-8f34-066fe5fb6cb8/String" />
          </type>
        </attributeProperty>
      </attributeProperties>
    </configurationSection>
  </configurationElements>
  <propertyValidators>
    <validators />
  </propertyValidators>
</configurationSectionModel>