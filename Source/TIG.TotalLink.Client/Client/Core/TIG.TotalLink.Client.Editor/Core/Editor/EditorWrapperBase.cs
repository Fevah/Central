using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using DevExpress.Entity.Model;
using DevExpress.Mvvm;
using DevExpress.Mvvm.UI.Native.ViewGenerator;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Core.Helper;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Client.Editor.Core.Editor
{
    public abstract class EditorWrapperBase : BindableBase
    {
        #region Private Fields

        private EditorDefinitionBase _editor;
        private readonly List<Func<object, object, string>> _validationMethods = new List<Func<object, object, string>>();

        #endregion


        #region Constructors

        protected EditorWrapperBase(System.Type type, IEdmPropertyInfo property)
        {
            InitializeFromProperty(type, property);
        }

        protected EditorWrapperBase(VisiblePropertyWrapper propertyWrapper)
        {
            if (propertyWrapper.ContainsAlias)
                InitializeFromAlias(propertyWrapper.OwnerType, propertyWrapper.Property, propertyWrapper.Alias);
            else
                InitializeFromProperty(propertyWrapper.OwnerType, propertyWrapper.Property);
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Indicates if this wrapper refers to a dynamic PersistentAlias field.
        /// </summary>
        public bool ContainsAlias
        {
            get { return (Alias != null); }
        }

        /// <summary>
        /// The property that this editor displays the value of.
        /// </summary>
        public IEdmPropertyInfo Property { get; private set; }

        /// <summary>
        /// Information about the related dynamic PersistentAlias for this property.
        /// </summary>
        public AliasedFieldMapping Alias { get; private set; }

        /// <summary>
        /// The definition of the editor to use for this field.
        /// </summary>
        public EditorDefinitionBase Editor
        {
            get { return _editor; }
            set
            {
                SetProperty(ref _editor, value, () => Editor, () =>
                    {
                        // When an Editor is assigned, store this wrapper on it as the parent
                        if (_editor != null)
                            _editor.Wrapper = this;
                    });
            }
        }

        /// <summary>
        /// The name of the property.
        /// If this wrapper is not an alias, then PropertyName and FieldName will be equal.
        /// If this wrapper is an alias, then PropertyName will contain the original property name, and FieldName will contain the alias field name.
        /// </summary>
        public string PropertyName { get; private set; }

        /// <summary>
        /// The name of the field.
        /// If this wrapper is not an alias, then PropertyName and FieldName will be equal.
        /// If this wrapper is an alias, then PropertyName will contain the original property name, and FieldName will contain the alias field name.
        /// </summary>
        public string FieldName { get; private set; }

        /// <summary>
        /// The Type that the property contains.
        /// If this wrapper is not an alias, then PropertyType and FieldType will be equal.
        /// If this wrapper is an alias, then PropertyType will contain the original property Type, and FieldType will contain the alias field Type.
        /// </summary>
        public System.Type PropertyType { get; private set; }

        /// <summary>
        /// The Type that the field contains.
        /// If this wrapper is not an alias, then PropertyType and FieldType will be equal.
        /// If this wrapper is an alias, then PropertyType will contain the original property Type, and FieldType will contain the alias field Type.
        /// </summary>
        public System.Type FieldType { get; private set; }

        /// <summary>
        /// The type that owns this property.
        /// </summary>
        public System.Type OwnerType { get; private set; }

        /// <summary>
        /// The base Type that declares this property.
        /// </summary>
        public System.Type DeclaringType { get; private set; }

        /// <summary>
        /// The display name of the field.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// The maximum allowable length of the field value. 
        /// </summary>
        public int MaxLength { get; set; }

        /// <summary>
        /// Indicates if the field is read-only.
        /// </summary>
        public bool IsReadOnly { get; set; }

        /// <summary>
        /// Indicates if the field is visible.
        /// </summary>
        public bool IsVisible { get; set; }

        /// <summary>
        /// Indicates if the field is required.
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// Indicates if the field can contain null.
        /// </summary>
        public bool AllowNull { get; set; }

        #endregion


        #region Public Methods

        /// <summary>
        /// Adds a validation method for this editor.
        /// </summary>
        /// <param name="validationMethod">The validation method to add.</param>
        public void AddValidation(Func<object, object, string> validationMethod)
        {
            _validationMethods.Add(validationMethod);
        }

        /// <summary>
        /// Determines if the supplied value can successfully be stored in this editor.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="context">The object that contains the value being validated.</param>
        /// <returns>The error message if any error was found; otherwise null.</returns>
        public virtual string Validate(object value, object context)
        {
            // Validate IsRequired
            if (IsRequired)
            {
                var stringValue = value as string;
                if (stringValue != null)
                {
                    // If the value is a string, make sure it does not just contain whitespace
                    if (string.IsNullOrWhiteSpace(stringValue))
                        return "Value is required.";
                }
                else
                {
                    // If the value is not a string, just test it for null
                    if (value == null)
                        return "Value is required.";
                }
            }

            // Validate MaxLength
            if (MaxLength > 0 && value != null)
            {
                var stringValue = value.ToString();
                if (stringValue.Length > MaxLength)
                    return string.Format("Value is longer than the maximum allowed length of {0}.", MaxLength);
            }

            // Execute each validation method
            if (context != null)
            {
                foreach (var validationMethod in _validationMethods)
                {
                    var errorMessage = validationMethod(value, context);
                    if (errorMessage != null)
                        return errorMessage;
                }
            }

            // If an editor is defined, perform editor specific validation
            if (Editor != null)
                return Editor.Validate(value);

            // Return null if there was no error
            return null;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Initializes the wrapper from an IEdmPropertyInfo.
        /// </summary>
        /// <param name="type">The type that owns this property.</param>
        /// <param name="property">The IEdmPropertyInfo to initialize from.</param>
        private void InitializeFromProperty(System.Type type, IEdmPropertyInfo property)
        {
            OwnerType = type;
            Property = property;

            CachePropertyDetails();
            GenerateDefaultEditor(PropertyType);
        }

        /// <summary>
        /// Initializes the wrapper from an AliasedFieldMapping.
        /// </summary>
        /// <param name="type">The type that owns this property.</param>
        /// <param name="property">The IEdmPropertyInfo for the related property.</param>
        /// <param name="alias">The AliasedFieldMapping to initialize from.</param>
        private void InitializeFromAlias(System.Type type, IEdmPropertyInfo property, AliasedFieldMapping alias)
        {
            OwnerType = type;
            Property = property;
            Alias = alias;

            CachePropertyDetails();

            FieldName = alias.AliasFieldName;
            FieldType = alias.TargetFieldType;

            GenerateDefaultEditor(PropertyType);
        }

        /// <summary>
        /// Generates a default editor for the specified datatype.
        /// </summary>
        /// <param name="type">The type to generate an editor for.</param>
        private void GenerateDefaultEditor(System.Type type)
        {
            TypeSwitch.On(type)
                .Case<string>(GenerateTextEditor)
                .Case<int>(GenerateSpinEditor)
                .Case<int?>(GenerateSpinEditor)
                .Case<long>(GenerateSpinEditor)
                .Case<long?>(GenerateSpinEditor)
                .Case<bool>(GenerateCheckboxEditor)
                .Case<bool?>(GenerateCheckboxEditor)
                .Case<Guid>(GenerateIdEditor)
                .Case<Guid?>(GenerateIdEditor)
                .Case<DateTime>(GenerateDateTimeEditor)
                .Case<DateTime?>(GenerateDateTimeEditor)
                .Case<ICommand>(GenerateButtonEditor)
                .Case<decimal>(GenerateDecimalEditor)
                .Case<decimal?>(GenerateDecimalEditor)
                .Case(typeof(ObservableCollection<>), GenerateListBoxEditor)
                .Case<DataObjectBase>(GenerateLookUpEditor)
                ;
        }

        /// <summary>
        /// Generates a new Text editor.
        /// </summary>
        private void GenerateTextEditor()
        {
            // Define a Text editor
            Editor = new TextEditorDefinition();

            // Default the length to 100 in case no size attribute is found
            MaxLength = 100;

            // Attempt to get the PropertyDescriptor for the property
            var propertyDescriptor = (Property == null ? null : Property.ContextObject as PropertyDescriptor);
            if (propertyDescriptor == null)
                return;

            // If the property has a SizeAttribute, set the length from the attribute value
            var sizeAttribute = propertyDescriptor.Attributes.OfType<SizeAttribute>().FirstOrDefault();
            if (sizeAttribute != null)
                MaxLength = (sizeAttribute.Size > -1 ? sizeAttribute.Size : 0);
        }

        /// <summary>
        /// Generates a new Spin editor.
        /// </summary>
        private void GenerateSpinEditor()
        {
            // Define a Spin editor
            Editor = new SpinEditorDefinition();
        }

        /// <summary>
        /// Generates a new Checkbox editor.
        /// </summary>
        private void GenerateCheckboxEditor()
        {
            // Define a Checkbox editor
            Editor = new CheckboxEditorDefinition();
        }

        /// <summary>
        /// Generates a new Id editor.
        /// </summary>
        private void GenerateIdEditor()
        {
            // Define an Id editor 
            Editor = new IdEditorDefinition();

            // Make the Oid column hidden by default
            if (FieldName.ToLower() == "oid")
                IsVisible = false;
        }

        /// <summary>
        /// Generates a new DateTime editor.
        /// </summary>
        private void GenerateDateTimeEditor()
        {
            // Define a DateTime editor 
            Editor = new DateTimeEditorDefinition();
        }

        /// <summary>
        /// Generates a new Button editor.
        /// </summary>
        private void GenerateButtonEditor()
        {
            // Define a Button editor 
            Editor = new ButtonEditorDefinition();
        }

        /// <summary>
        /// Generates a new Decimal editor.
        /// </summary>
        private void GenerateDecimalEditor()
        {
            // Define a Decimal editor 
            Editor = new DecimalEditorDefinition();
        }

        /// <summary>
        /// Generates a new ListBox editor.
        /// </summary>
        private void GenerateListBoxEditor()
        {
            // Define a ListBox editor 
            Editor = new ListBoxEditorDefinition();
        }

        /// <summary>
        /// Generates a new LookUp editor.
        /// </summary>
        private void GenerateLookUpEditor(System.Type propertyType)
        {
            // Define a LookUp editor 
            Editor = new LookUpEditorDefinition()
            {
                EntityType = propertyType
            };
        }

        /// <summary>
        /// Caches required property details based on the property info and its attributes.
        /// </summary>
        private void CachePropertyDetails()
        {
            PropertyName = Property.Name;
            FieldName = Property.Name;
            PropertyType = Property.PropertyType;
            FieldType = Property.PropertyType;
            DeclaringType = ((PropertyDescriptor)Property.ContextObject).ComponentType;
            DisplayName = (Property.Attributes.Name ?? Property.DisplayName.RemoveUnderscores().AddSpaces());
            MaxLength = Property.Attributes.MaxLength();
            IsReadOnly = Property.Attributes.IsReadOnly ?? Property.IsReadOnly;
            IsVisible = !Property.Attributes.Hidden();
            IsRequired = Property.Attributes.Required();
        }

        #endregion


        #region Overrides

        public override string ToString()
        {
            if (ContainsAlias)
                return string.Format("({0}) {1} = {2}", DeclaringType, FieldName, PropertyName);

            return string.Format("({0}) {1}", DeclaringType, FieldName);
        }

        #endregion
    }
}
