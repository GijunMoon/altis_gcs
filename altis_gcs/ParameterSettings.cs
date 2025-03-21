using System.Collections.Generic;

namespace altis_gcs
{
    public class ParameterSettings
    {
        public List<string> ParameterOrder { get; set; } = new List<string>();

        public int ParameterCount => ParameterOrder.Count;
    }
}