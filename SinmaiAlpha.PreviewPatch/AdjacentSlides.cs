using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class AdjacentSlides
{
    // Restore the user's existing adjacent-key extension only. All other
    // 0.6.0 parser methods and grammar remain from the supplied new runtime.
    public static void Apply(string target, string donor)
    {
        using var resolver=new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(target));
        resolver.AddSearchDirectory(Path.GetDirectoryName(donor));
        resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location));
        var parameters=new ReaderParameters{AssemblyResolver=resolver};
        using var source=AssemblyDefinition.ReadAssembly(donor,parameters);
        using var dest=AssemblyDefinition.ReadAssembly(target,parameters);
        var module=dest.MainModule;
        // Published MajdataEdit uses ReadyToRun; discard native precompiled
        // sections when rewriting IL, as Cecil cannot emit mixed-mode code.
        module.Attributes |= ModuleAttributes.ILOnly;
        IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> types) => types.SelectMany(t=>new[]{t}.Concat(Types(t.NestedTypes)));
        var types=Types(module.Types).ToDictionary(t=>t.FullName);
        TypeReference Type(TypeReference t) => types.TryGetValue(t.FullName,out var mapped)?mapped:module.ImportReference(t);
        object Map(object value) => value switch {
            MethodReference m when types.TryGetValue(m.DeclaringType.FullName,out var t) => t.Methods.Single(x=>x.FullName==m.FullName),
            FieldReference f when types.TryGetValue(f.DeclaringType.FullName,out var t) => t.Fields.Single(x=>x.Name==f.Name),
            MethodReference m => module.ImportReference(m),
            FieldReference f => module.ImportReference(f),
            TypeReference t => Type(t),
            _ => value
        };
        var validator=source.MainModule.Types.Single(t=>t.Name=="SlideSyntaxValidator").Methods.Single(m=>m.Name=="ValidateShape");
        var path=source.MainModule.Types.Single(t=>t.Name=="SlidePathParser");
        var predicate=Types(new[]{path}).SelectMany(t=>t.Methods).Single(m=>m.HasBody && m.Parameters.Count==1 && m.Parameters[0].ParameterType.Name=="SlidePathSegmentData" && m.Name.Contains("b__"));
        foreach(var old in new[]{validator,predicate})
        {
            var fresh=types[old.DeclaringType.FullName].Methods.Single(m=>m.Name==old.Name && m.Parameters.Count==old.Parameters.Count);
            var body=new MethodBody(fresh){InitLocals=old.Body.InitLocals,MaxStackSize=old.Body.MaxStackSize};
            foreach(var v in old.Body.Variables) body.Variables.Add(new VariableDefinition(Type(v.VariableType)));
            var map=old.Body.Instructions.ToDictionary(i=>i,i=>Instruction.Create(OpCodes.Nop));
            foreach(var i in old.Body.Instructions)
            {
                var n=map[i]; n.OpCode=i.OpCode;
                n.Operand=i.Operand switch {
                    Instruction jump=>map[jump], Instruction[] jumps=>jumps.Select(j=>map[j]).ToArray(),
                    VariableDefinition v=>body.Variables[v.Index], ParameterDefinition p=>fresh.Parameters[p.Index],
                    null=>null, _=>Map(i.Operand)
                }; body.Instructions.Add(n);
            }
            if(old.Body.ExceptionHandlers.Count!=0) throw new InvalidOperationException("Unexpected adjacent-slide method handlers");
            fresh.Body=body; Console.WriteLine("Preserved adjacent straight: "+fresh.FullName);
        }
        dest.Write(target+".patched"); dest.Dispose(); File.Move(target+".patched",target,true);
    }
}
