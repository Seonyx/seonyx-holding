using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ContentAnalysisEngine
{
    public class AnalysisReport
    {
        [JsonProperty("schemaVersion")]
        public string SchemaVersion { get; set; } = "1.0";

        [JsonProperty("generatedAt")]
        public DateTime GeneratedAt { get; set; }

        [JsonProperty("chapterId")]
        public string ChapterId { get; set; }

        [JsonProperty("sourceFile")]
        public string SourceFile { get; set; }

        [JsonProperty("configuration")]
        public AnalysisConfiguration Configuration { get; set; }

        [JsonProperty("metrics")]
        public MetricsSummary Metrics { get; set; }

        [JsonProperty("flaggedWords")]
        public List<FlaggedWord> FlaggedWords { get; set; }

        [JsonProperty("flaggedNgrams")]
        public List<FlaggedNgram> FlaggedNgrams { get; set; }

        [JsonProperty("proximityEchoes")]
        public List<ProximityEcho> ProximityEchoes { get; set; }
    }

    public class MetricsSummary
    {
        [JsonProperty("totalWords")]
        public int TotalWords { get; set; }

        [JsonProperty("uniqueWords")]
        public int UniqueWords { get; set; }

        [JsonProperty("typeTokenRatio")]
        public double TypeTokenRatio { get; set; }

        [JsonProperty("movingAverageTTR")]
        public double MovingAverageTTR { get; set; }

        [JsonProperty("hapaxCount")]
        public int HapaxCount { get; set; }

        [JsonProperty("hapaxRatio")]
        public double HapaxRatio { get; set; }
    }

    public class FlaggedWord
    {
        [JsonProperty("word")]
        public string Word { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("zScore")]
        public double ZScore { get; set; }

        [JsonProperty("pids")]
        public List<PidCount> Pids { get; set; }
    }

    public class PidCount
    {
        [JsonProperty("pid")]
        public string Pid { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }
    }

    public class FlaggedNgram
    {
        [JsonProperty("phrase")]
        public string Phrase { get; set; }

        [JsonProperty("ngramSize")]
        public int NgramSize { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("normalisedRate")]
        public double NormalisedRate { get; set; }

        [JsonProperty("pids")]
        public List<string> Pids { get; set; }
    }

    public class ProximityEcho
    {
        [JsonProperty("term")]
        public string Term { get; set; }

        [JsonProperty("pidA")]
        public string PidA { get; set; }

        [JsonProperty("pidB")]
        public string PidB { get; set; }

        [JsonProperty("distanceWords")]
        public int DistanceWords { get; set; }

        [JsonProperty("severity")]
        public string Severity { get; set; }
    }

    public class HapaxResult
    {
        public int           HapaxCount { get; set; }
        public double        HapaxRatio { get; set; }
        public List<string>  HapaxWords { get; set; }
    }
}
