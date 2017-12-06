using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Azure.WebJobs.Host;
using System.Text;
using ClassLibrary1;
using System.Reflection;
//using System.ComponentModel.DataAnnotations;
using Microsoft.Azure.WebJobs;

namespace MyAnalyzer
{
#if false
    
    For each attribute, invoke the "Validate" on it. 

    What extensions are loaded? 

    Can we instantiate the attribute?

    Attr Type mismatch?
    1. 


#endif

    public class TTT
    {

        public static void Foo()
        {
            var x = new JobHostConfiguration();
        }
    }


    // Can't access the workspace. 
    // https://stackoverflow.com/questions/23203206/roslyn-current-workspace-in-diagnostic-with-code-fix-project

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MyAnalyzerAnalyzer : DiagnosticAnalyzer
    {
        internal const string Category = "WebJobs";
                
        private static DiagnosticDescriptor Rule1 = new DiagnosticDescriptor(
            "WJ0001",
            "Illegal binding type",
            "Can't bind attribute '{0}' to parameter type '{1}'. Possible options are:{2}",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Description #1");

        private static DiagnosticDescriptor Rule2 = new DiagnosticDescriptor(
                  "WJ0001",
                  "Illegal binding type",
                   "{0} can't be value '{1}': {2}",
                    Category,
                  DiagnosticSeverity.Warning,
                  isEnabledByDefault: true,
                  description: "Description #2");


        // $$$ This should be scoped to per-project 
        IJobHostMetadataProvider _tooling;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule1, Rule2); } }

        // Test we can load webjobs 
        public static void VerifyWebJobsLoaded()
        {
            var x = new JobHostConfiguration();
        }

        public override void Initialize(AnalysisContext context)
        {
            // AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            VerifyWebJobsLoaded();

            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            // context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
            //context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InvocationExpression);

            context.RegisterSyntaxNodeAction(AnalyzeNode2, SyntaxKind.MethodDeclaration);

            context.RegisterCompilationStartAction(compilationAnalysisContext =>
            {
                var compilation = compilationAnalysisContext.Compilation;

                AssemblyCache.Instance.Build(compilation);
                this._tooling = AssemblyCache.Instance.Tooling;

                // cast to PortableExecutableReference which has a file path
                var x1 = compilation.References.OfType<PortableExecutableReference>().ToArray();
                var webJobsPath = (from reference in x1
                                   where IsWebJobsSdk(reference)
                                   select reference.FilePath).Single();

                var analyzer = new WebJobsAnalyzer(webJobsPath);
                compilationAnalysisContext.RegisterSyntaxNodeAction(
                    sytaxNodeAnalysisContext => analyzer.Analyze(sytaxNodeAnalysisContext),
                    SyntaxKind.Attribute);
            });


            {
                //Microsoft.Azure.WebJobs.JobHostConfiguration hostConfig = new Microsoft.Azure.WebJobs.JobHostConfiguration();
                //this._tooling = hostConfig.CreateMetadataProvider();
                //this._tooling = AssemblyCache.Instance.Tooling;
            }
        }

        static Dictionary<string, Assembly> _asms = new Dictionary<string, Assembly>();

        static MyAnalyzerAnalyzer()
        {
            Init();
        }

        static void AddAsm(Assembly a)
        {
            _asms[a.GetName().Name] = a;
        }

        static void Init()
        {
            //var a = typeof(JobHostConfiguration).Assembly;
            //_asms[a.GetName().Name] = a;
            // AddAsm(typeof(ValidationAttribute).Assembly); // Resolve to our copy since we reflect over it.
        }

        private Dictionary<string, string> _asmLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "netstandard", "netstandard.dll" },
            { "Microsoft.WindowsAzure.Storage",  "Microsoft.WindowsAzure.Storage.dll" },
            
            //  This VS Proj refers to : System.ComponentModel.*Data*Annotations
            // C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.1\System.ComponentModel.DataAnnotations.dll
            // But the extensions refer to:
            //   System.ComponentModel.Annotations
            // C:\Program Files\dotnet\sdk\NuGetFallbackFolder\system.componentmodel.annotations\4.4.0\ref\netstandard2.0\System.ComponentModel.Annotations.dll

            { "System.ComponentModel.Annotations", "System.ComponentModel.Annotations.dll" }            
        };

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name);

            Assembly a;
            if (_asms.TryGetValue(name.Name, out a))
            {
                return a;
            }

            // $$$
            // Microsoft.WindowsAzure.Storage !!!  is in the .Core directory, but not in the .VSIX directory 

            // $$$ Super hack. 
            // The VS Build system copies the files (notably netstandard.dll) to the build's \bin\debug output. 
            // But it does not get copied at runtime. 
            string dllName;
            if (_asmLookup.TryGetValue(name.Name, out dllName))
            {
                //var path = @"C:\dev\afunc\core\azure-webjobs-sdk\src\Analyzer\WebJobs.VSAnalyzer.Vsix\bin\Debug\" + dllName;
                //var path = @"C:\dev\afunc\core\azure-webjobs-sdk\src\Analyzer\WebJobs.VSAnalyzer.Core\bin\Debug\" + dllName;

                var path = @"C:\dev\afunc\core\azure-webjobs-sdk\src\Microsoft.Azure.WebJobs.Host\bin\Debug\netstandard2.0\publish\" + dllName;

                if (System.IO.File.Exists(path))
                {
                    a = Assembly.LoadFrom(path);
                    _asms[a.GetName().Name] = a;
                    return a;
                }
            }

            return null;
        }

        private bool IsWebJobsSdk(PortableExecutableReference reference)
        {
            if (reference.FilePath.EndsWith("Microsoft.Azure.WebJobs.dll"))
            {
                return true;
            }
            return false;
        }

        //  This is called extremely frequently 
        private void AnalyzeNode2(SyntaxNodeAnalysisContext context)
        {
            if (_tooling == null) // Not yet initialized 
            {
                return;
            }
            var methodDecl = (MethodDeclarationSyntax)context.Node;
            var methodName = methodDecl.Identifier.ValueText;


            // Look at referenced assemblies. 
            if (false)
            {
                // $$$ No symbol - from users code. 
                var sym = context.SemanticModel.GetSymbolInfo(methodDecl);
                var sym2 = sym.Symbol;
                var asm = sym2.ContainingAssembly; // Assembly the user's method is defined in.

                foreach (var mod in asm.Modules)
                {
                    // Get referenced assemblies. 
                    foreach (var asm2 in mod.ReferencedAssemblySymbols)
                    {
                        var x = IsSdkAsssembly(asm2);
                    }
                }
            }


            // Go through 
            var parameterList = methodDecl.ParameterList;

            foreach (var param in parameterList.Parameters)
            {
                foreach (var attrList in param.AttributeLists)
                {
                    foreach (var attr in attrList.Attributes)
                    {
                        // For each attr. 
                        //  [Blob("container/blob")]
                        //  [Microsoft.Azure.WebJobs.BlobAttribute("container/blob")]

                                             
                        // Named args?



                        var sym = context.SemanticModel.GetSymbolInfo(attr);

                        var sym2 = sym.Symbol;
                        if (sym2 == null)
                        {
                            return; // compilation error
                        }

                        var attrType = sym2.ContainingType;

                        var attrNamespace = attrType.ContainingNamespace.ToString(); // "Microsoft.Azure.WebJobs"
                        var attrName = attrType.Name; // "BlobAttribute"

                        // if (attrName == "BlobAttribute")
                        {
                            // No symbol for the parameter; just the parameter's type
                            var paramSym = context.SemanticModel.GetSymbolInfo(param.Type);
                            var p2 = paramSym.Symbol;

                            if (p2 == null)
                            {
                                return;
                            }

                            try
                            {
                                Type fakeType = Class1.MakeFakeType(p2); // throws if can't convert. 

                                if (param.IsOutParameter())
                                {
                                    fakeType = fakeType.MakeByRefType();
                                }

                                Attribute result = Class1.MakeAttr(_tooling, context.SemanticModel, attr);

                                // Report errors from invalid attribute properties. 
                                ValidateAttribute(result, context, attr);                                

                                var errors = _tooling.CheckBindingErrors(result, fakeType);

                                if (errors != null)
                                {
                                    var sb = new StringBuilder();
                                    foreach (var possible in errors)
                                    {
                                        sb.Append("\n  " + possible);
                                    }
                                    var diagnostic =
                                        Diagnostic.Create(Rule1,
                                        param.GetLocation(), 
                                        result.GetType().Name,
                                        fakeType.ToString(), 
                                        sb.ToString());

                                    context.ReportDiagnostic(diagnostic);
                                }
                            }
                            catch (Exception e)
                            {
                                return;
                            }
                        }

                        // check if base type is a binding? 
                        // Scan for [Binding] attribute on this type. 
                        var attr2 = attrType.GetAttributes();
                        foreach (var x in attr2)
                        {
                            var @namespace = x.AttributeClass.ContainingNamespace.ToString();
                            var @name = x.AttributeClass.Name;

                            // If "Binding", then ok. 
                        }
                    }
                }
            }
        }

        // Given an instantiated attribute, run the validators on it and report back any errors. 
        private void ValidateAttribute(Attribute result, SyntaxNodeAnalysisContext context, AttributeSyntax attrSyntax)
        {
            SemanticModel semantics = context.SemanticModel;
            var t = result.GetType();

            IMethodSymbol symAttributeCtor = (IMethodSymbol)semantics.GetSymbolInfo(attrSyntax).Symbol;
            var syntaxParams = symAttributeCtor.Parameters;

            int idx = 0;
            foreach (var arg in attrSyntax.ArgumentList.Arguments)
            {

                string argName = null;
                if (arg.NameColon != null)
                {
                    argName = arg.NameColon.Name.ToString();
                }
                else if (arg.NameEquals != null)
                {
                    argName = arg.NameEquals.Name.ToString();
                }
                else
                {
                    argName = syntaxParams[idx].Name; // Positional 
                }

                var propInfo = t.GetProperty(argName, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);
                if (propInfo != null)
                {
                    var value = propInfo.GetValue(result);

                    // Validate 
                    {
                        // var attrs = propInfo.GetCustomAttributes<ValidationAttribute>();

                        var attrs = propInfo.GetCustomAttributes();

                        foreach (var attr in attrs)
                        {
                            try
                            {
                                var method = attr.GetType().GetMethod("Validate", new Type[] { typeof(object), typeof(string) });
                                if (method != null)
                                {
                                    // attr.Validate(value, propInfo.Name);
                                    try
                                    {
                                        method.Invoke(attr, new object[] { value, propInfo.Name });
                                    }
                                    catch(TargetInvocationException te)
                                    {
                                        throw te.InnerException;
                                    }
                                }

                                //attr.Validate(value, propInfo.Name);
                            }                            
                            catch (Exception e)
                            {

                                // throw new InvalidOperationException($"'{propInfo.Name}' can't be '{value}': {e.Message}");
                                
                                var diagnostic =
                                    Diagnostic.Create(Rule2,
                                    arg.GetLocation(),
                                    propInfo.Name,
                                    value,
                                    e.Message
                                    );

                                context.ReportDiagnostic(diagnostic);
                            }
                        }
                    }
                }


                idx++;
            }            
        }

        private bool IsSdkAsssembly(IAssemblySymbol asm)
        {
            foreach (var mod in asm.Modules)
            {
                // Get referenced assemblies. 
                foreach (var asm2 in mod.ReferencedAssemblySymbols)
                {
                    // Is SDK? 

                }
            }
            return false;
        }
    }
}
