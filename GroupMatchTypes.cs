namespace KdSoft.Quartz.AspNet
{
    /// <summary>
    /// Specifies how group names should be matched. Comparisons are not case-sensitive.
    /// </summary>
    public static class GroupMatchTypes
    {
        /// <summary>Group name must be equal to specified text.</summary>
        public const string Equal = "";
        /// <summary>Group name must begin with specified text.</summary>
        public const string StartsWith = "STARTSWITH";
        /// <summary>Group name must end with specified text.</summary>
        public const string EndsWith = "ENDSWITH";
        /// <summary>Group name must contain specified text.</summary>
        public const string Contains = "CONTAINS";
    }
}
