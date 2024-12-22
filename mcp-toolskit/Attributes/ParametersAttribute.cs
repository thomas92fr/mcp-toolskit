using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mcp_toolskit.Attributes
{
    /// <summary>
    /// Attribut personnalisé pour décrire les paramètres
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class ParametersAttribute : Attribute
    {
        public string[] ParameterDescriptions { get; }

        public ParametersAttribute(params string[] parameterDescriptions)
        {
            ParameterDescriptions = parameterDescriptions;
        }
    }

}
