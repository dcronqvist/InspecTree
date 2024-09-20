using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace InspecTree.Generator
{
  public class OverloadToSourceConverter : IOverloadToSourceConverter
  {
    public IEnumerable<SourceFile> ConvertToSource(IReadOnlyCollection<GeneratedOverload> overloads)
    {
      foreach (var overload in overloads)
      {
        var fileName = $"{overload.NamespaceName}_{overload.ClassName}_{overload.MethodName}_Overload.g.cs";
        var usingLines = overload.Usings.Select(u => $"using {u};");

        var source =
        $@"
{string.Join("\n", usingLines)}

namespace {overload.NamespaceName}
{{
  {IncludePartialIfNeeded(overload.ClassAccessModifier)} class {overload.ClassName}
  {{
    {overload.MethodAccessModifier} {overload.ReturnType} {overload.MethodName}({ConvertParameters(overload.Parameters)})
    {{
      /* Empty overload that can be intercepted */
      {(overload.ReturnType == "void" ? "return;" : "return default;")}
    }}
  }}
}}".Trim();

        yield return new SourceFile(fileName, SourceText.From(source, Encoding.UTF8));
      }
    }

    private string IncludePartialIfNeeded(string classAccessModifier) =>
      classAccessModifier.EndsWith("partial") ? classAccessModifier : $"{classAccessModifier} partial";

    private string ConvertParameters(IEnumerable<GeneratedParameter> parameters) =>
      string.Join(", ", parameters.Select(p => $"{p.ParameterType} {p.ParameterName}"));
  }
}
