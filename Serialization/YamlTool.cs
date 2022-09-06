using GitHubManagement.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GitHubManagement.Serialization;

public static class YamlTool
{
    public static Dictionary<string, RACRepositoryConfig> GetRepositories(string yamlContents)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(SnakeCaseNamingConvention.Instance)
            .Build();
        return deserializer.Deserialize<Dictionary<string, RACRepositoryConfig>>(yamlContents);
    }

    /// <summary>
    /// Parses the repos yaml file
    /// </summary>
    public static Dictionary<string, RACRepositoryConfig> GetRepositoriesFromFile(string filePath)
    {
        //var a = Fn.Yamldecode(filePath);
        var yamlContents = File.ReadAllText(filePath);
        return GetRepositories(yamlContents);
    }
}

public class SnakeCaseNamingConvention : INamingConvention
{
    public static readonly INamingConvention Instance = new SnakeCaseNamingConvention();
    private INamingConvention _underScore;

    public SnakeCaseNamingConvention()
    {
        _underScore = UnderscoredNamingConvention.Instance;
    }

    public string Apply(string value) => _underScore.Apply(value).ToLower();
}
