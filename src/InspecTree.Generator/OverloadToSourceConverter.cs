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
        var source = $@"
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace {overload.NamespaceName}
{{
  {overload.ClassAccessModifier} partial class {overload.ClassName}
  {{
    {overload.MethodAccessModifier}{(overload.IsStatic ? " static" : "")} {overload.ReturnType} {overload.MethodName}({ConvertParameters(overload.Parameters)})
    {{
      /* Empty overload that can be intercepted */
    }}
  }}
}}";

        yield return new SourceFile(fileName, SourceText.From(source, Encoding.UTF8));
      }
    }

    private string ConvertParameters(IEnumerable<GeneratedParameter> parameters) =>
      string.Join(", ", parameters.Select(p => $"{p.ParameterType} {p.ParameterName}"));
  }
}
