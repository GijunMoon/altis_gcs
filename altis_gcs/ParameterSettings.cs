using System.Collections.Generic;

namespace altis_gcs
{
    /// <summary>
    /// 통신 방식을 정의하는 열거형 (문자열 기반 또는 이진 데이터 기반)
    /// </summary>
    public enum CommunicationType
    {
        Text,
        Binary
    }

    public class ParameterSettings
    {
        /// <summary>
        /// CSV 또는 Binary 데이터의 파라미터 순서를 저장합니다.
        /// </summary>
        public List<string> ParameterOrder { get; set; } = new List<string>();

        /// <summary>
        /// 설정된 파라미터의 개수를 반환합니다.
        /// </summary>
        public int ParameterCount => ParameterOrder.Count;

        /// <summary>
        /// 현재 설정된 통신 방식을 저장. 기본값은 Text.
        /// </summary>
        public CommunicationType CommType { get; set; } = CommunicationType.Text;
    }
}
