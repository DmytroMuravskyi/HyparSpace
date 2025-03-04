// This code was generated by Hypar.
// Edits to this code will be overwritten the next time you run 'hypar init'.
// DO NOT EDIT THIS FILE.

using Elements;
using Elements.GeoJSON;
using Elements.Geometry;
using Elements.Geometry.Solids;
using Elements.Validators;
using Elements.Serialization.JSON;
using Hypar.Functions;
using Hypar.Functions.Execution;
using Hypar.Functions.Execution.AWS;
using Hypar.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using Line = Elements.Geometry.Line;
using Polygon = Elements.Geometry.Polygon;

namespace DefineProgramRequirements
{
    #pragma warning disable // Disable all warnings

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.1.21.0 (Newtonsoft.Json v13.0.0.0)")]
    
    public  class DefineProgramRequirementsInputs : S3Args
    
    {
        [Newtonsoft.Json.JsonConstructor]
        
        public DefineProgramRequirementsInputs(bool @showAdvancedOptions, IList<ProgramRequirement> @programRequirements, string bucketName, string uploadsBucket, Dictionary<string, string> modelInputKeys, string gltfKey, string elementsKey, string ifcKey):
        base(bucketName, uploadsBucket, modelInputKeys, gltfKey, elementsKey, ifcKey)
        {
            var validator = Validator.Instance.GetFirstValidatorForType<DefineProgramRequirementsInputs>();
            if(validator != null)
            {
                validator.PreConstruct(new object[]{ @showAdvancedOptions, @programRequirements});
            }
        
            this.ShowAdvancedOptions = @showAdvancedOptions;
            this.ProgramRequirements = @programRequirements;
        
            if(validator != null)
            {
                validator.PostConstruct(this);
            }
        }
    
        [Newtonsoft.Json.JsonProperty("Show Advanced Options", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public bool ShowAdvancedOptions { get; set; } = false;
    
        [Newtonsoft.Json.JsonProperty("Program Requirements", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public IList<ProgramRequirement> ProgramRequirements { get; set; }
    
    }
}