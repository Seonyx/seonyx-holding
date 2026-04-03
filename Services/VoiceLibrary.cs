using System.Collections.Generic;

namespace Seonyx.Web.Services
{
    public class VoiceInfo
    {
        public string VoiceId     { get; set; }  // e.g. "en_GB-alan-medium"
        public string DisplayName { get; set; }  // e.g. "Alan"
        public string AccentLabel { get; set; }  // e.g. "British English"
        public string Gender      { get; set; }  // "male" | "female"
        public string Quality     { get; set; }  // "low" | "medium" | "high"
        public int?   SpeakerId   { get; set; }  // null for single-speaker voices
    }

    public static class VoiceLibrary
    {
        public static readonly List<VoiceInfo> All = new List<VoiceInfo>
        {
            // British English
            new VoiceInfo
            {
                VoiceId = "en_GB-alan-medium", DisplayName = "Alan",
                AccentLabel = "British English", Gender = "male", Quality = "medium"
            },
            new VoiceInfo
            {
                VoiceId = "en_GB-jenny_dioco-medium", DisplayName = "Jenny",
                AccentLabel = "British English", Gender = "female", Quality = "medium"
            },
            new VoiceInfo
            {
                VoiceId = "en_GB-cori-high", DisplayName = "Cori",
                AccentLabel = "British English", Gender = "female", Quality = "high"
            },

            // American English
            new VoiceInfo
            {
                VoiceId = "en_US-ryan-high", DisplayName = "Ryan",
                AccentLabel = "American English", Gender = "male", Quality = "high"
            },
            new VoiceInfo
            {
                VoiceId = "en_US-amy-medium", DisplayName = "Amy",
                AccentLabel = "American English", Gender = "female", Quality = "medium"
            },
            new VoiceInfo
            {
                VoiceId = "en_US-lessac-high", DisplayName = "Lessac",
                AccentLabel = "American English", Gender = "female", Quality = "high"
            },

            // Australian English
            new VoiceInfo
            {
                VoiceId = "en_AU-wombat-medium", DisplayName = "Wombat",
                AccentLabel = "Australian English", Gender = "male", Quality = "medium"
            },
        };
    }
}
