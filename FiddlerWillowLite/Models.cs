using System.Collections.Generic;
using System.Runtime.Serialization;

namespace FiddlerWillowLite
{
    /// <summary>一条域名到目标地址的映射规则。</summary>
    [DataContract]
    public class HostRule
    {
        [DataMember]
        public string Domain { get; set; }

        [DataMember]
        public string Target { get; set; }

        [DataMember]
        public bool Enabled { get; set; }

        public HostRule()
        {
            Enabled = true;
        }

        public override string ToString()
        {
            return (Domain ?? "") + "  ->  " + (Target ?? "");
        }
    }

    /// <summary>一个可整体启停的环境项目，例如 Dev / Test / Prod。</summary>
    [DataContract]
    public class EnvProject
    {
        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public bool Enabled { get; set; }

        [DataMember]
        public List<HostRule> Rules { get; set; }

        public EnvProject()
        {
            Rules = new List<HostRule>();
            Enabled = false;
        }

        public override string ToString()
        {
            return Name ?? "(未命名项目)";
        }
    }

    /// <summary>持久化到磁盘的插件配置。</summary>
    [DataContract]
    public class WillowConfig
    {
        [DataMember]
        public bool MappingEnabled { get; set; }

        [DataMember]
        public List<EnvProject> Projects { get; set; }

        public WillowConfig()
        {
            MappingEnabled = true;
            Projects = new List<EnvProject>();
        }
    }
}
