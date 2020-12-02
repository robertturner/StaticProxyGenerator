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

        public static bool ImplementMethodsBothExplicitAndClassPublic = false;

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new IfceSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is not IfceSyntaxReceiver receiver)
                return;

            foreach (InterfaceDeclarationSyntax ifce in receiver.CandidateFields)
            {
                if (ifce.AttributeLists.Where(a => a.ToString().StartsWith("[StaticProxyGenerate")).Any())
                {
                    var comp = context.Compilation;
                    var assy = comp.Assembly;

                    var nsDecl = ifce.SyntaxTree.GetRoot().DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
                    if (nsDecl == null)
                        throw new ArgumentException("Missing namespace declaration");
                    var ns = nsDecl.Name;
                    var className = ifce.Identifier + "Implementation";

                    int? numGenArgs = null;
                    if (ifce.TypeParameterList != null)
                        numGenArgs = ifce.TypeParameterList.Parameters.Count;

                    var declType = numGenArgs.HasValue ? assy.GetTypeByMetadataName($"{ns}.{ifce.Identifier}`{numGenArgs}") : assy.GetTypeByMetadataName($"{ns}.{ifce.Identifier}");
                    var members = declType.GetMembers();

                    var extraIfces = new List<INamedTypeSymbol>();
                    var attrs = declType.GetAttributes();
                    foreach (var attr in attrs)
                    {
                        var ctorArgs = attr.ConstructorArguments;
                        foreach (var arg in ctorArgs)
                        {
                            var vals = arg.Values;
                            foreach (var v in vals)
                            {
                                var b = v.Value;
                                if (b is INamedTypeSymbol ifceType && ifceType.TypeKind == TypeKind.Interface)
                                    extraIfces.Add(ifceType);
                            }
                        }
                    }

                    var ifces = declType.AllInterfaces.Concat(extraIfces).ToArray();
                    var ifceMappingsIdxes = new Dictionary<string, int>(ifces.Length + 1);
                    var allMembers = members.Concat(ifces.SelectMany(ifce => ifce.GetMembers()));
                    var allMethods = allMembers.Where(m => m.Kind == SymbolKind.Method).Select(m => (IMethodSymbol)m).ToArray();
                    var ifceMappingsList = new List<string>(ifceMappingsIdxes.Count);
                    foreach (var m in allMethods)
                    {
                        //var contTypeStr = m.ContainingType.IsGenericType ? m.ContainingType.ConstructUnboundGenericType().ToString() : m.ContainingType.ToString();
                        var contTypeStr = m.ContainingType.ToString();
                        if (!ifceMappingsIdxes.ContainsKey(contTypeStr))
                        {
                            ifceMappingsIdxes.Add(contTypeStr, ifceMappingsList.Count);
                            ifceMappingsList.Add(contTypeStr);
                        }
                    }
                    var ifceMappingsText = string.Join(", ", ifceMappingsList.Select(ifceName => $"typeof({className}{ifce.TypeParameterList}).GetInterfaceMap(typeof({ifceName}))"));
                    var extraIfcesDecl = string.Join(", ", extraIfces.Select(ifce => $"{ifce.ContainingNamespace}.{ifce.Name}"));
                    if (!string.IsNullOrEmpty(extraIfcesDecl))
                        extraIfcesDecl = ", " + extraIfcesDecl;

                    var implementationBuilder = new StringBuilder();
                    foreach (var node in ifce.SyntaxTree.GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>())
                        implementationBuilder.AppendLine(node.ToString());
                    implementationBuilder.Append(@$"
namespace {ns}
{{
    ///
    /// Auto-generated implementation for {ifce.Identifier}
    ///
    public sealed class {className}{ifce.TypeParameterList}: {ifce.Identifier}{ifce.TypeParameterList}{extraIfcesDecl} {ifce.ConstraintClauses}
    {{
        private readonly StaticProxyInterfaces.InterceptorHandler interceptorHandler;
        private static readonly System.Reflection.MethodInfo[] methodInfos = new System.Reflection.MethodInfo[{allMethods.Length}];
        private static readonly System.Reflection.InterfaceMapping[] ifceMappings = new[] 
        {{
            {ifceMappingsText}
        }};
        public {className}(StaticProxyInterfaces.InterceptorHandler interceptorHandler)
        {{
            if (interceptorHandler == null)
                throw new System.ArgumentNullException(nameof(interceptorHandler));
            this.interceptorHandler = interceptorHandler;
        }}
");
                    uint methodIndex = 0;
                    foreach (var m in allMethods)
                    {
                        var genericDef = m.IsGenericMethod ? $"<{string.Join(", ", m.TypeParameters.Select(t => t.Name.ToString()))}>" : "";
                        var genericConstraintText = m.IsGenericMethod ? string.Join(" ", m.TypeParameters.Select(t =>
                        {
                            string ret = null;
                            void appendConstraint(string str) => ret = (ret == null) ? str : (ret + ", " + str);
                            if (t.HasNotNullConstraint)
                                appendConstraint("notnull");
                            if (t.HasReferenceTypeConstraint)
                                appendConstraint("class");
                            if (t.HasValueTypeConstraint)
                                appendConstraint("struct");
                            if (t.HasUnmanagedTypeConstraint)
                                appendConstraint("unmanaged");
                            foreach (var typeConstraint in t.ConstraintTypes)
                                appendConstraint(typeConstraint.Name);
                            return (ret != null) ? $" where {t.Name} : {ret}" : "";
                        })) : "";
                        var genericParametersList = m.IsGenericMethod ? string.Join(", ", m.TypeParameters.Select(t => $"typeof({t.Name})")) : "";

                        var parametersDeclList = string.Join(", ", m.Parameters.Select(p => $"{p.Type} {p.Name}"));
                        var parametersArray = string.Join(", ", m.Parameters.Select(p => p.Name.ToString()));

                        var returnLine = m.ReturnsVoid ? "" : $"return ({m.ReturnType})";
                        var ifceReturn = m.ReturnsVoid ? "" : "return ";
                        var ifceMappingIdx = ifceMappingsIdxes[m.ContainingType.ToString()];

                        var methodSigProlog = ImplementMethodsBothExplicitAndClassPublic ? $"public {m.ReturnType} " : $"{m.ReturnType} {m.ContainingType}.";
                        var primaryMethodGenericConstraints = ImplementMethodsBothExplicitAndClassPublic ? genericConstraintText : "";

                        implementationBuilder.Append($@"
        // MethodInfo index: {methodIndex}
        {methodSigProlog}{m.Name}{genericDef}({parametersDeclList}) {primaryMethodGenericConstraints}
        {{
            if (methodInfos[{methodIndex}] == null)
            {{
                var classMethod = (System.Reflection.MethodInfo)System.Reflection.MethodBase.GetCurrentMethod();
                var index = System.Array.IndexOf(ifceMappings[{ifceMappingIdx}].TargetMethods, classMethod);
                methodInfos[{methodIndex}] = (index >= 0) ? ifceMappings[{ifceMappingIdx}].InterfaceMethods[index] : classMethod;
            }}
            var genericParameters = new System.Type[] {{ {genericParametersList} }};
            var args = new object[] {{ {parametersArray} }};
            {returnLine}interceptorHandler(this, methodInfos[{methodIndex}], args, genericParameters);
        }}
");
                        if (ImplementMethodsBothExplicitAndClassPublic)
                            implementationBuilder.Append($@"
        {m.ReturnType} {m.ContainingType}.{m.Name}{genericDef}({parametersDeclList})
        {{
            {ifceReturn}this.{m.Name}{genericDef}({parametersArray});
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
