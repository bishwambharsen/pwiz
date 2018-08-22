using System;
using System.ComponentModel;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.ElementLocators
{
    public abstract class DocumentProperty
    {
        protected DocumentProperty(string name)
        {
            Name = name;
        }
        public string Name { get; }
        public abstract Type ComponentType { get; }
        public abstract Type ValueType { get; }
        public abstract object GetValueFromNode(SkylineObject node);
        public abstract void SetValue(SkylineObject node, object value);
    }

    public class DocumentProperty<TComponent, TValue> : DocumentProperty where TComponent : SkylineObject
    {
        private Func<TComponent, TValue> _getter;
        private Action<TComponent, TValue> _setter;
        public DocumentProperty(string name, Func<TComponent, TValue> getter, Action<TComponent, TValue> setter) : base(name)
        {
            _getter = getter;
            _setter = setter;
        }

        public override object GetValueFromNode(SkylineObject node)
        {
            return _getter((TComponent) node);
        }

        public override Type ComponentType
        {
            get { return typeof(TComponent); }
        }

        public override void SetValue(SkylineObject node, object value)
        {
            _setter((TComponent) node, (TValue) value);
        }

        public override Type ValueType
        {
            get { return typeof(TValue); }
        }
    }

    public class ColumnProperty : DocumentProperty
    {
        public ColumnProperty(string name, ColumnDescriptor columnDescriptor) : base(name)
        {
            ColumnDescriptor = columnDescriptor;
        }

        public ColumnDescriptor ColumnDescriptor { get; private set; }

        public override Type ComponentType
        {
            get { return ColumnDescriptor.Parent.PropertyType; }
        }

        public override object GetValueFromNode(SkylineObject node)
        {
            return ColumnDescriptor.GetPropertyValue(new RowItem(node), null);
        }

        public override void SetValue(SkylineObject node, object value)
        {
            ColumnDescriptor.SetValue(new RowItem(node), null, value);
        }

        public override Type ValueType
        {
            get { return ColumnDescriptor.PropertyType; }
        }
    }
}
