using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.ElementLocators
{
    public class DocumentProperties
    {
        public DocumentProperties(SkylineDataSchema skylineDataSchema)
        {
            DataSchema = skylineDataSchema;
        }

        public IEnumerable<ColumnDescriptor> GetProperties<TComponent>(params string[] names)
        {
            var allProperties = DataSchema.GetPropertyDescriptors(typeof(TComponent)).ToDictionary(pd => pd.Name);
        }

        public IEnumerable<ColumnDescriptor> GetProperties(Type componentType, IEnumerable<PropertyPath> propertyPaths)
        {
            var rootColumn = ColumnDescriptor.RootColumn(DataSchema, componentType);
            var viewSpec = new ViewSpec().SetColumns(propertyPaths.Select(pp => new ColumnSpec(pp)));
            var viewInfo = new ViewInfo(rootColumn, viewSpec);
            return viewInfo.AllColumnDescriptors;
        }

        public SkylineDataSchema DataSchema { get; private set; }

        public IEnumerable<PropertyDescriptor> ListProperties(Type t)
        {
            
        }


        ImmutableList<DocumentProperty> PROPERTIES = ImmutableList.ValueOf(new DocumentProperty[]
        {

            new DocumentProperty<TransitionDocNode, bool>(
                "Quantitative", docNode => docNode.Quantitative, (docNode, value) => docNode.ChangeQuantitative(value)),
            new DocumentProperty<ChromatogramSet, string>(
                "SampleType", chromSet => chromSet.SampleType.ToString(),
                (chromSet, value) => chromSet.ChangeSampleType(SampleType.FromName(value))),
            new DocumentProperty<ChromatogramSet, double?>(
                "AnalyteConcentration", chromSet => chromSet.AnalyteConcentration,
                (chromSet, value) => chromSet.ChangeAnalyteConcentration(value)),
            new DocumentProperty<ChromatogramSet, double>(
                "SampleDilutionFactory", chromSet=>chromSet.SampleDilutionFactor, (chromSet, value)=>chromSet.ChangeDilutionFactor(value)),

        });
    }
}
