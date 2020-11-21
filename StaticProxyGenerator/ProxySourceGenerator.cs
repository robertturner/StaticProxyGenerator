using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace StaticProxyGenerator
{
    [Generator]
    public class ProxySourceGenerator : ISourceGenerator
    {
        class IfceSyntaxReceiver : ISyntaxReceiver
        {
            public List<InterfaceDeclarationSyntax> CandidateFields { get; } = new List<InterfaceDeclarationSyntax>();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is InterfaceDeclarationSyntax ifceDeclarationSyntax && ifceDeclarationSyntax.AttributeLists.Count > 0)
                    CandidateFields.Add(ifceDeclarationSyntax);
            }
        }

        public void Initialize(InitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new IfceSyntaxReceiver());
        }

        public void Execute(SourceGeneratorContext context)
        {
            if (!(context.SyntaxReceiver is IfceSyntaxReceiver receiver))
                return;

            foreach (InterfaceDeclarationSyntax ifce in receiver.CandidateFields)
            {
                if (ifce.AttributeLists.Where(a => a.ToString() == "[StaticProxyGenerate]").Any())
                {
                    var nsDecl = ifce.SyntaxTree.GetRoot().DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
                    if (nsDecl == null)
                        throw new ArgumentException("Missing namespace declaration");
                    var ns = nsDecl.Name;
                    var className = ifce.Identifier + "Implementation";

                    var implementationBuilder = new StringBuilder();

                    foreach (var node in ifce.SyntaxTree.GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>())
                        implementationBuilder.AppendLine(node.ToString());
                    implementationBuilder.Append(@$"
namespace {ns}
{{
    ///
    /// Auto-generated implementation for {ifce.Identifier}
    ///
    public sealed class {className} : {ifce.Identifier}
    {{
        private readonly StaticProxyInterfaces.InterceptorHandler interceptorHandler;
        private static readonly System.Reflection.MethodInfo[] methodInfos = new System.Reflection.MethodInfo[{ifce.Members.Count}];
        private static readonly System.Reflection.InterfaceMapping ifceMapping = typeof({className}).GetInterfaceMap(typeof({ifce.Identifier}));
        public {className}(StaticProxyInterfaces.InterceptorHandler interceptorHandler)
        {{
            if (interceptorHandler == null)
                throw new System.ArgumentNullException(nameof(interceptorHandler));
            this.interceptorHandler = interceptorHandler;
        }}

");
                    uint methodIndex = 0;
                    foreach (var member in ifce.Members)
                    {
                        var method = (MethodDeclarationSyntax)member;
                        var genericTypesDef = method.DescendantNodes().OfType<TypeParameterListSyntax>().FirstOrDefault();
                        var genericDef = (genericTypesDef != null) ? genericTypesDef.ToFullString() : "";
                        var genericConstraint = method.DescendantNodes().OfType<TypeParameterConstraintClauseSyntax>().FirstOrDefault();
                        var genericConstraintText = (genericConstraint != null) ? genericConstraint.ToFullString() : "";
                        var parametersDeclList = string.Join(", ", method.ParameterList.Parameters.Select(p => $"{p.Type} {p.Identifier}"));
                        var genericParametersList = string.Join(", ", method.DescendantNodes().OfType<TypeParameterSyntax>().Select(t => $"typeof({t.Identifier})"));
                        var parametersArray = string.Join(", ", method.ParameterList.Parameters.Select(p => p.Identifier));

                        implementationBuilder.Append($@"
        // MethodInfo index: {methodIndex}
        public {method.ReturnType} {method.Identifier}{genericDef}({parametersDeclList}) {genericConstraintText}
        {{
            if (methodInfos[{methodIndex}] == null)
            {{
                var classMethod = (System.Reflection.MethodInfo)System.Reflection.MethodBase.GetCurrentMethod();
                var index = System.Array.IndexOf(ifceMapping.TargetMethods, classMethod);
                methodInfos[{methodIndex}] = ifceMapping.InterfaceMethods[index];
            }}
            var genericParameters = new System.Type[] {{ {genericParametersList} }};
            var args = new object[] {{ {parametersArray} }};
            return ({method.ReturnType})interceptorHandler(methodInfos[{methodIndex}], args, genericParameters);
        }}
");
                        methodIndex++;
                    }
                    implementationBuilder.Append(@"

    }
}
");
                    context.AddSource(className, SourceText.From(implementationBuilder.ToString(), Encoding.UTF8));
                }
            }
        }

    }
}
