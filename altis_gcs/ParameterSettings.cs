using System.Collections.Generic;

namespace altis_gcs
{
    public class ParameterSettings
    {
        public List<string> ParameterOrder { get; set; } = new List<string>();

        public int ParameterCount => ParameterOrder.Count; // 파라미터 개수 반환
    }
}
