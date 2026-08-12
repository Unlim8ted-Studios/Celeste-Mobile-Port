using Mono.Cecil;
using Mono.Cecil.Cil;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var dll = Path.Combine(root, "CelesteRuntime", "_framework", "Celeste.Wasm.mm.ubh2gjnetc.dll");
var backup = Path.Combine(root, "CelesteRuntime", "_framework", "Celeste.Wasm.mm.ubh2gjnetc.dll.bak-relink-initmmflags");
var tmp = dll + ".tmp";

if (!File.Exists(backup)) {
    File.Copy(dll, backup);
}
if (File.Exists(tmp)) {
    File.Delete(tmp);
}

var rp = new ReaderParameters { InMemory = true };
using var module = ModuleDefinition.ReadModule(dll, rp);

RemoveSocketRelinks(module);
RewriteCallingConventionScope(module);
RewriteInitMMFlags(module);

module.Write(tmp);
File.Copy(tmp, dll, overwrite: true);
File.Delete(tmp);

Console.WriteLine("Patched Celeste.Wasm.mm socket relinks and InitMMFlags");

foreach (var relativePath in new[] {
    Path.Combine("CelesteRuntime", "celeste", "Celeste.dll"),
    Path.Combine("CelesteRuntime", "celeste", "Celeste.Mod.mm.dll"),
    Path.Combine("CelesteRuntime", "celeste", "Everest", "Celeste.Mod.mm.dll")
}) {
    PatchSplashFields(Path.Combine(root, relativePath));
}

Console.WriteLine("Patched EverestSplashHandler callback fields");

foreach (var relativePath in new[] {
    Path.Combine("CelesteRuntime", "celeste", "Celeste.dll"),
    Path.Combine("CelesteRuntime", "celeste", "Celeste.Mod.mm.dll"),
    Path.Combine("CelesteRuntime", "celeste", "Everest", "Celeste.Mod.mm.dll")
}) {
    PatchLoaderFileSystemWatcher(Path.Combine(root, relativePath));
}

Console.WriteLine("Patched Everest Loader FileSystemWatcher startup");

foreach (var relativePath in new[] {
    Path.Combine("CelesteRuntime", "celeste", "Celeste.dll"),
    Path.Combine("CelesteRuntime", "celeste", "Celeste.Mod.mm.dll"),
    Path.Combine("CelesteRuntime", "celeste", "Everest", "Celeste.Mod.mm.dll")
}) {
    PatchEverestWasmStartupThreads(Path.Combine(root, relativePath));
}

Console.WriteLine("Patched Everest WASM startup background threads");

foreach (var relativePath in new[] {
    Path.Combine("CelesteRuntime", "celeste", "Celeste.dll"),
    Path.Combine("CelesteRuntime", "celeste", "Celeste.Mod.mm.dll"),
    Path.Combine("CelesteRuntime", "celeste", "Everest", "Celeste.Mod.mm.dll")
}) {
    PatchCelesteWasmThreadStarts(Path.Combine(root, relativePath));
}

Console.WriteLine("Patched Celeste WASM game thread starts");

foreach (var relativePath in new[] {
    Path.Combine("CelesteRuntime", "celeste", "Celeste.dll"),
    Path.Combine("CelesteRuntime", "celeste", "Celeste.Mod.mm.dll"),
    Path.Combine("CelesteRuntime", "celeste", "Everest", "Celeste.Mod.mm.dll")
}) {
    PatchEverestWorkerThreadTaskScheduler(Path.Combine(root, relativePath));
}

Console.WriteLine("Patched Everest worker task scheduler");

static void RemoveSocketRelinks(ModuleDefinition module) {
    var rules = module.GetTypes().First(t => t.FullName == "MonoMod.MonoModRules");
    var cctor = rules.Methods.First(m => m.Name == ".cctor");
    foreach (var target in new[] { "System.Net.Sockets.Socket", "System.Net.Sockets.NetworkStream" }) {
        var instructions = cctor.Body.Instructions;
        for (var i = 0; i < instructions.Count - 3; i++) {
            if (instructions[i].OpCode == OpCodes.Ldstr &&
                (string?)instructions[i].Operand == target &&
                instructions[i + 1].OpCode == OpCodes.Ldtoken &&
                instructions[i + 2].OpCode == OpCodes.Call &&
                instructions[i + 3].OpCode == OpCodes.Call) {
                var il = cctor.Body.GetILProcessor();
                il.Remove(instructions[i + 3]);
                il.Remove(instructions[i + 2]);
                il.Remove(instructions[i + 1]);
                il.Remove(instructions[i]);
                break;
            }
        }
    }
}

static void RewriteCallingConventionScope(ModuleDefinition module) {
    var coreLib = module.AssemblyReferences.FirstOrDefault(r => r.Name == "System.Private.CoreLib");

    if (coreLib == null) {
        coreLib = new AssemblyNameReference(
            "System.Private.CoreLib",
            new Version(10, 0, 0, 0)
        ) {
            PublicKeyToken = new byte[] {
                0x7c,
                0xec,
                0x85,
                0xd7,
                0xbe,
                0xa7,
                0x79,
                0x8e
            }
        };

        module.AssemblyReferences.Add(coreLib);
    }

    foreach (var typeReference in module.GetTypeReferences()) {
        if (
            typeReference.FullName == "System.Runtime.InteropServices.CallingConvention" &&
            typeReference.Scope is AssemblyNameReference scope &&
            scope.Name == "System.Runtime.InteropServices"
        ) {
            typeReference.Scope = coreLib;
        }
    }
}

static void RewriteInitMMFlags(ModuleDefinition module) {
    var relinker = module.GetTypes().First(t => t.FullName == "Celeste.Mod.patch_Everest/patch_Relinker");
    var method = relinker.Methods.First(m => m.Name == "InitMMFlags");

    var originalOperands = method.Body.Instructions.Select(i => i.Operand).ToArray();
    var dependencyDirs = originalOperands.OfType<FieldReference>().First(f => f.Name == "DependencyDirs");
    var mods = originalOperands.OfType<FieldReference>().First(f => f.Name == "Mods");
    var removePatchReferences = originalOperands.OfType<FieldReference>().First(f => f.Name == "RemovePatchReferences");
    var origInitMMFlags = relinker.Methods.First(m => m.Name == "orig_InitMMFlags");
    var seedDependencyCache = relinker.Methods.First(m => m.Name == "SeedDependencyCache");
    var mapDependencies = originalOperands.OfType<MethodReference>().First(m =>
        m.Name == "MapDependencies" &&
        m.Parameters.Count == 1 &&
        m.Parameters[0].ParameterType.FullName == "Mono.Cecil.ModuleDefinition");

    var readModule = originalOperands.OfType<MethodReference>().First(m =>
        m.Name == "ReadModule" &&
        m.Parameters.Count == 1 &&
        m.Parameters[0].ParameterType.FullName == "System.String");
    var addMethods = originalOperands.OfType<MethodReference>().Where(m => m.Name == "Add").ToArray();
    var listStringAdd = addMethods.First(m => m.DeclaringType.FullName.Contains("System.String"));
    var listModuleReferenceAdd = addMethods.First(m => m.DeclaringType.FullName.Contains("Mono.Cecil.ModuleReference"));

    var body = method.Body;
    body.Instructions.Clear();
    body.ExceptionHandlers.Clear();
    body.Variables.Clear();
    body.InitLocals = true;
    body.Variables.Add(new VariableDefinition(module.ImportReference(typeof(ModuleDefinition))));

    var il = body.GetILProcessor();
    il.Append(il.Create(OpCodes.Ldarg_0));
    il.Append(il.Create(OpCodes.Call, origInitMMFlags));
    il.Append(il.Create(OpCodes.Ldarg_0));
    il.Append(il.Create(OpCodes.Ldfld, dependencyDirs));
    il.Append(il.Create(OpCodes.Ldstr, "/bin/"));
    il.Append(il.Create(OpCodes.Callvirt, listStringAdd));
    AppendSeedModModule("/bin/Celeste.Wasm.mm.dll");
    AppendSeedDependencyModule("/bin/mscorlib.dll");
    AppendSeedDependencyModule("/bin/netstandard.dll");
    AppendSeedDependencyModule("/bin/System.Private.CoreLib.dll");
    AppendSeedDependencyModule("/bin/System.Runtime.dll");
    AppendSeedDependencyModule("/bin/System.Runtime.InteropServices.dll");
    il.Append(il.Create(OpCodes.Ldarg_0));
    il.Append(il.Create(OpCodes.Ldc_I4_0));
    il.Append(il.Create(OpCodes.Stfld, removePatchReferences));
    il.Append(il.Create(OpCodes.Ret));

    void AppendSeedModModule(string path) {
        il.Append(il.Create(OpCodes.Ldstr, path));
        il.Append(il.Create(OpCodes.Call, readModule));
        il.Append(il.Create(OpCodes.Stloc_0));
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldfld, mods));
        il.Append(il.Create(OpCodes.Ldloc_0));
        il.Append(il.Create(OpCodes.Callvirt, listModuleReferenceAdd));
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldloc_0));
        il.Append(il.Create(OpCodes.Call, seedDependencyCache));
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldloc_0));
        il.Append(il.Create(OpCodes.Callvirt, mapDependencies));
    }

    void AppendSeedDependencyModule(string path) {
        il.Append(il.Create(OpCodes.Ldstr, path));
        il.Append(il.Create(OpCodes.Call, readModule));
        il.Append(il.Create(OpCodes.Stloc_0));
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldloc_0));
        il.Append(il.Create(OpCodes.Call, seedDependencyCache));
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldloc_0));
        il.Append(il.Create(OpCodes.Callvirt, mapDependencies));
    }
}

static void PatchSplashFields(string dll) {
    var backup = dll + ".bak-splash-fields";
    var tmp = dll + ".tmp";

    if (!File.Exists(backup)) {
        File.Copy(dll, backup);
    }
    if (File.Exists(tmp)) {
        File.Delete(tmp);
    }

    var resolver = new DefaultAssemblyResolver();
    var dllDir = Path.GetDirectoryName(dll)!;
    resolver.AddSearchDirectory(dllDir);
    resolver.AddSearchDirectory(Path.GetFullPath(Path.Combine(dllDir, "..")));
    resolver.AddSearchDirectory(Path.Combine(dllDir, "Everest"));
    var framework = Path.GetFullPath(Path.Combine(dllDir, "..", "_framework"));
    if (Directory.Exists(framework)) {
        resolver.AddSearchDirectory(framework);
    }

    var rp = new ReaderParameters {
        InMemory = true,
        AssemblyResolver = resolver
    };
    using var module = ModuleDefinition.ReadModule(dll, rp);
    var splash = module.GetTypes().FirstOrDefault(t => t.FullName == "Celeste.Mod.EverestSplashHandler");
    if (splash == null) {
        return;
    }

    var action = module.ImportReference(typeof(Action));
    var actionString = module.ImportReference(typeof(Action<string>));
    AddFieldIfMissing(splash, "start", action);
    AddFieldIfMissing(splash, "send", actionString);
    AddFieldIfMissing(splash, "stop", action);

    module.Write(tmp);
    File.Copy(tmp, dll, overwrite: true);
    File.Delete(tmp);
}

static void PatchLoaderFileSystemWatcher(string dll) {
    var backup = dll + ".bak-no-filesystem-watcher";
    var tmp = dll + ".tmp";

    if (!File.Exists(backup)) {
        File.Copy(dll, backup);
    }
    if (File.Exists(tmp)) {
        File.Delete(tmp);
    }

    var resolver = new DefaultAssemblyResolver();
    var dllDir = Path.GetDirectoryName(dll)!;
    resolver.AddSearchDirectory(dllDir);
    resolver.AddSearchDirectory(Path.GetFullPath(Path.Combine(dllDir, "..")));
    resolver.AddSearchDirectory(Path.Combine(dllDir, "Everest"));
    var framework = Path.GetFullPath(Path.Combine(dllDir, "..", "_framework"));
    if (Directory.Exists(framework)) {
        resolver.AddSearchDirectory(framework);
    }

    var rp = new ReaderParameters {
        InMemory = true,
        AssemblyResolver = resolver
    };
    using var module = ModuleDefinition.ReadModule(dll, rp);
    var loader = GetAllModuleTypes(module).FirstOrDefault(t => t.FullName == "Celeste.Mod.Everest/Loader");
    var loadAuto = loader?.Methods.FirstOrDefault(m => m.Name == "LoadAuto" && m.HasBody);

    if (loadAuto == null) {
        return;
    }

    var instructions = loadAuto.Body.Instructions;
    var watcherStart = instructions.FirstOrDefault(i =>
        i.OpCode == OpCodes.Newobj &&
        i.Operand is MethodReference method &&
        method.DeclaringType.FullName == "System.IO.FileSystemWatcher");

    if (watcherStart == null) {
        return;
    }

    var watcherIndex = instructions.IndexOf(watcherStart);
    var leave = instructions
        .Skip(watcherIndex)
        .FirstOrDefault(i => i.OpCode.FlowControl == FlowControl.Branch && i.OpCode.Code == Code.Leave_S);

    if (leave?.Operand is not Instruction retTarget) {
        return;
    }

    var setAutoLoadNewMods = loader!.Methods.First(m => m.Name == "set_AutoLoadNewMods");
    var importedSetAutoLoadNewMods = module.ImportReference(setAutoLoadNewMods);

    instructions[watcherIndex].OpCode = OpCodes.Ldc_I4_0;
    instructions[watcherIndex].Operand = null;
    instructions[watcherIndex + 1].OpCode = OpCodes.Call;
    instructions[watcherIndex + 1].Operand = importedSetAutoLoadNewMods;
    instructions[watcherIndex + 2].OpCode = OpCodes.Leave_S;
    instructions[watcherIndex + 2].Operand = retTarget;

    for (var i = watcherIndex + 3; i < instructions.IndexOf(leave); i++) {
        instructions[i].OpCode = OpCodes.Nop;
        instructions[i].Operand = null;
    }

    module.Write(tmp);
    File.Copy(tmp, dll, overwrite: true);
    File.Delete(tmp);
}

static void PatchEverestWasmStartupThreads(string dll) {
    var backup = dll + ".bak-no-startup-threads";
    var tmp = dll + ".tmp";

    if (!File.Exists(backup)) {
        File.Copy(dll, backup);
    }
    if (File.Exists(tmp)) {
        File.Delete(tmp);
    }

    var resolver = new DefaultAssemblyResolver();
    var dllDir = Path.GetDirectoryName(dll)!;
    resolver.AddSearchDirectory(dllDir);
    resolver.AddSearchDirectory(Path.GetFullPath(Path.Combine(dllDir, "..")));
    resolver.AddSearchDirectory(Path.Combine(dllDir, "Everest"));
    var framework = Path.GetFullPath(Path.Combine(dllDir, "..", "_framework"));
    if (Directory.Exists(framework)) {
        resolver.AddSearchDirectory(framework);
    }

    var rp = new ReaderParameters {
        InMemory = true,
        AssemblyResolver = resolver
    };
    using var module = ModuleDefinition.ReadModule(dll, rp);
    var types = GetAllModuleTypes(module).ToArray();
    var changed = false;

    var debugRc = types.FirstOrDefault(t => t.FullName == "Celeste.Mod.Everest/DebugRC");
    var debugRcInitialize = debugRc?.Methods.FirstOrDefault(m => m.Name == "Initialize" && m.HasBody);
    if (debugRcInitialize != null) {
        ReplaceWithReturn(debugRcInitialize);
        changed = true;
    }

    var updater = types.FirstOrDefault(t => t.FullName == "Celeste.Mod.Everest/Updater");
    var requestAll = updater?.Methods.FirstOrDefault(m =>
        m.Name == "RequestAll" &&
        m.HasBody &&
        m.ReturnType.FullName == "System.Threading.Tasks.Task");
    if (requestAll != null) {
        ReplaceWithCompletedTask(module, requestAll);
        changed = true;
    }

    var modUpdaterHelper = types.FirstOrDefault(t => t.FullName == "Celeste.Mod.Helpers.ModUpdaterHelper");
    var runAsyncCheckForModUpdates = modUpdaterHelper?.Methods.FirstOrDefault(m =>
        m.Name == "RunAsyncCheckForModUpdates" &&
        m.HasBody);
    if (runAsyncCheckForModUpdates != null) {
        ReplaceWithReturn(runAsyncCheckForModUpdates);
        changed = true;
    }

    var discordSdk = types.FirstOrDefault(t => t.FullName == "Celeste.Mod.Everest/DiscordSDK");
    var createDiscordInstance = discordSdk?.Methods.FirstOrDefault(m =>
        m.Name == "CreateInstance" &&
        m.HasBody);
    if (createDiscordInstance != null) {
        ReplaceWithNullReturn(createDiscordInstance);
        changed = true;
    }

    var loadRichPresenceIcons = discordSdk?.Methods.FirstOrDefault(m =>
        m.Name == "LoadRichPresenceIcons" &&
        m.HasBody);
    if (loadRichPresenceIcons != null) {
        ReplaceWithReturn(loadRichPresenceIcons);
        changed = true;
    }

    if (!changed) {
        return;
    }

    module.Write(tmp);
    File.Copy(tmp, dll, overwrite: true);
    File.Delete(tmp);
}

static void PatchCelesteWasmThreadStarts(string dll) {
    var backup = dll + ".bak-no-game-thread-starts";
    var tmp = dll + ".tmp";

    if (!File.Exists(backup)) {
        File.Copy(dll, backup);
    }
    if (File.Exists(tmp)) {
        File.Delete(tmp);
    }

    var resolver = new DefaultAssemblyResolver();
    var dllDir = Path.GetDirectoryName(dll)!;
    resolver.AddSearchDirectory(dllDir);
    resolver.AddSearchDirectory(Path.GetFullPath(Path.Combine(dllDir, "..")));
    resolver.AddSearchDirectory(Path.Combine(dllDir, "Everest"));
    var framework = Path.GetFullPath(Path.Combine(dllDir, "..", "_framework"));
    if (Directory.Exists(framework)) {
        resolver.AddSearchDirectory(framework);
    }

    var rp = new ReaderParameters {
        InMemory = true,
        AssemblyResolver = resolver
    };
    using var module = ModuleDefinition.ReadModule(dll, rp);
    var types = GetAllModuleTypes(module).ToArray();
    var changed = false;

    foreach (var runThreadName in new[] { "Celeste.RunThread", "Celeste.patch_RunThread" }) {
        var runThread = types.FirstOrDefault(t => t.FullName == runThreadName);
        var start = runThread?.Methods.FirstOrDefault(m =>
            m.Name == "Start" &&
            m.HasBody &&
            m.Parameters.Count >= 1 &&
            m.Parameters[0].ParameterType.FullName == "System.Action");
        if (start != null) {
            ReplaceWithActionInvoke(start);
            changed = true;
        }
    }

    var splash = types.FirstOrDefault(t => t.FullName == "Celeste.Mod.EverestSplashHandler");
    var stopSplash = splash?.Methods.FirstOrDefault(m => m.Name == "StopSplash" && m.HasBody);
    if (stopSplash != null) {
        ReplaceWithReturn(stopSplash);
        changed = true;
    }

    if (!changed) {
        return;
    }

    module.Write(tmp);
    File.Copy(tmp, dll, overwrite: true);
    File.Delete(tmp);
}

static void PatchEverestWorkerThreadTaskScheduler(string dll) {
    var backup = dll + ".bak-inline-worker-scheduler";
    var tmp = dll + ".tmp";

    if (!File.Exists(backup)) {
        File.Copy(dll, backup);
    }
    if (File.Exists(tmp)) {
        File.Delete(tmp);
    }

    var resolver = new DefaultAssemblyResolver();
    var dllDir = Path.GetDirectoryName(dll)!;
    resolver.AddSearchDirectory(dllDir);
    resolver.AddSearchDirectory(Path.GetFullPath(Path.Combine(dllDir, "..")));
    resolver.AddSearchDirectory(Path.Combine(dllDir, "Everest"));
    var framework = Path.GetFullPath(Path.Combine(dllDir, "..", "_framework"));
    if (Directory.Exists(framework)) {
        resolver.AddSearchDirectory(framework);
    }

    var rp = new ReaderParameters {
        InMemory = true,
        AssemblyResolver = resolver
    };
    using var module = ModuleDefinition.ReadModule(dll, rp);
    var types = GetAllModuleTypes(module).ToArray();
    var scheduler = types.FirstOrDefault(t => t.FullName == "Celeste.Mod.Helpers.WorkerThreadTaskScheduler");
    if (scheduler == null) {
        return;
    }

    var tryExecuteTask = scheduler.Methods
        .Where(m => m.HasBody)
        .SelectMany(m => m.Body.Instructions)
        .Select(i => i.Operand)
        .OfType<MethodReference>()
        .FirstOrDefault(m =>
            m.Name == "TryExecuteTask" &&
            m.DeclaringType.FullName == "System.Threading.Tasks.TaskScheduler");
    if (tryExecuteTask == null) {
        return;
    }

    var changed = false;

    var queueTask = scheduler.Methods.FirstOrDefault(m => m.Name == "QueueTask" && m.HasBody);
    if (queueTask != null) {
        ReplaceWithTryExecuteTask(queueTask, tryExecuteTask, returnsBool: false);
        changed = true;
    }

    var tryExecuteTaskInline = scheduler.Methods.FirstOrDefault(m => m.Name == "TryExecuteTaskInline" && m.HasBody);
    if (tryExecuteTaskInline != null) {
        ReplaceWithTryExecuteTask(tryExecuteTaskInline, tryExecuteTask, returnsBool: true);
        changed = true;
    }

    var ctor = scheduler.Methods.FirstOrDefault(m =>
        m.Name == ".ctor" &&
        m.HasBody &&
        m.Parameters.Count == 2 &&
        m.Parameters[0].ParameterType.FullName == "System.String" &&
        m.Parameters[1].ParameterType.FullName == "System.Boolean");
    if (ctor != null) {
        NopThreadStartCallWithReceiver(ctor);
        changed = true;
    }

    var dispose = scheduler.Methods.FirstOrDefault(m => m.Name == "Dispose" && m.HasBody);
    if (dispose != null) {
        NopThreadJoinCallWithReceiver(dispose);
        changed = true;
    }

    var staHelper = types.FirstOrDefault(t => t.FullName == "Celeste.Mod.STAThreadHelper");
    var staCtor = staHelper?.Methods.FirstOrDefault(m => m.Name == ".ctor" && m.HasBody);
    if (staCtor != null) {
        NopThreadStartCallWithReceiver(staCtor);
        NopThreadSetApartmentStateCallWithReceiver(staCtor);
        changed = true;
    }

    if (!changed) {
        return;
    }

    module.Write(tmp);
    File.Copy(tmp, dll, overwrite: true);
    File.Delete(tmp);
}

static void ReplaceWithReturn(MethodDefinition method) {
    var body = method.Body;
    body.Instructions.Clear();
    body.ExceptionHandlers.Clear();
    body.Variables.Clear();
    body.InitLocals = false;
    body.GetILProcessor().Append(Instruction.Create(OpCodes.Ret));
}

static void ReplaceWithCompletedTask(ModuleDefinition module, MethodDefinition method) {
    var completedTask = module.ImportReference(
        typeof(System.Threading.Tasks.Task).GetProperty(nameof(System.Threading.Tasks.Task.CompletedTask))!.GetMethod!);

    var body = method.Body;
    body.Instructions.Clear();
    body.ExceptionHandlers.Clear();
    body.Variables.Clear();
    body.InitLocals = false;
    var il = body.GetILProcessor();
    il.Append(Instruction.Create(OpCodes.Call, completedTask));
    il.Append(Instruction.Create(OpCodes.Ret));
}

static void ReplaceWithNullReturn(MethodDefinition method) {
    var body = method.Body;
    body.Instructions.Clear();
    body.ExceptionHandlers.Clear();
    body.Variables.Clear();
    body.InitLocals = false;
    var il = body.GetILProcessor();
    il.Append(Instruction.Create(OpCodes.Ldnull));
    il.Append(Instruction.Create(OpCodes.Ret));
}

static void ReplaceWithActionInvoke(MethodDefinition method) {
    var invoke = method.Module.ImportReference(typeof(Action).GetMethod(nameof(Action.Invoke))!);

    var body = method.Body;
    body.Instructions.Clear();
    body.ExceptionHandlers.Clear();
    body.Variables.Clear();
    body.InitLocals = false;
    var il = body.GetILProcessor();
    il.Append(Instruction.Create(OpCodes.Ldarg_0));
    il.Append(Instruction.Create(OpCodes.Callvirt, invoke));
    il.Append(Instruction.Create(OpCodes.Ret));
}

static void ReplaceWithTryExecuteTask(MethodDefinition method, MethodReference tryExecuteTask, bool returnsBool) {
    var body = method.Body;
    body.Instructions.Clear();
    body.ExceptionHandlers.Clear();
    body.Variables.Clear();
    body.InitLocals = false;
    var il = body.GetILProcessor();
    il.Append(Instruction.Create(OpCodes.Ldarg_0));
    il.Append(Instruction.Create(OpCodes.Ldarg_1));
    il.Append(Instruction.Create(OpCodes.Call, tryExecuteTask));
    if (!returnsBool) {
        il.Append(Instruction.Create(OpCodes.Pop));
    }
    il.Append(Instruction.Create(OpCodes.Ret));
}

static void NopThreadStartCallWithReceiver(MethodDefinition method) {
    var instructions = method.Body.Instructions;
    for (var i = 0; i < instructions.Count; i++) {
        if (
            instructions[i].OpCode == OpCodes.Callvirt &&
            instructions[i].Operand is MethodReference called &&
            called.DeclaringType.FullName == "System.Threading.Thread" &&
            called.Name == "Start"
        ) {
            Nop(instructions[i]);
            for (var j = i - 1; j >= Math.Max(0, i - 3); j--) {
                if (instructions[j].OpCode == OpCodes.Ldarg_0 || instructions[j].OpCode == OpCodes.Ldfld) {
                    Nop(instructions[j]);
                    continue;
                }
                break;
            }
        }
    }
}

static void NopThreadJoinCallWithReceiver(MethodDefinition method) {
    var instructions = method.Body.Instructions;
    for (var i = 0; i < instructions.Count; i++) {
        if (
            instructions[i].OpCode == OpCodes.Callvirt &&
            instructions[i].Operand is MethodReference called &&
            called.DeclaringType.FullName == "System.Threading.Thread" &&
            called.Name == "Join" &&
            called.Parameters.Count == 0
        ) {
            Nop(instructions[i]);
            for (var j = i - 1; j >= Math.Max(0, i - 3); j--) {
                if (instructions[j].OpCode == OpCodes.Ldarg_0 || instructions[j].OpCode == OpCodes.Ldfld) {
                    Nop(instructions[j]);
                    continue;
                }
                break;
            }
        }
    }
}

static void NopThreadSetApartmentStateCallWithReceiver(MethodDefinition method) {
    var instructions = method.Body.Instructions;
    for (var i = 0; i < instructions.Count; i++) {
        if (
            instructions[i].OpCode == OpCodes.Callvirt &&
            instructions[i].Operand is MethodReference called &&
            called.DeclaringType.FullName == "System.Threading.Thread" &&
            called.Name == "SetApartmentState"
        ) {
            Nop(instructions[i]);
            for (var j = i - 1; j >= Math.Max(0, i - 4); j--) {
                if (instructions[j].OpCode == OpCodes.Ldarg_0 || instructions[j].OpCode == OpCodes.Ldfld || instructions[j].OpCode == OpCodes.Ldc_I4_0) {
                    Nop(instructions[j]);
                    continue;
                }
                break;
            }
        }
    }
}

static void Nop(Instruction instruction) {
    instruction.OpCode = OpCodes.Nop;
    instruction.Operand = null;
}

static IEnumerable<TypeDefinition> GetAllModuleTypes(ModuleDefinition module) {
    foreach (var type in module.Types) {
        foreach (var nested in GetAllTypes(type)) {
            yield return nested;
        }
    }
}

static IEnumerable<TypeDefinition> GetAllTypes(TypeDefinition type) {
    yield return type;

    foreach (var nestedType in type.NestedTypes) {
        foreach (var nested in GetAllTypes(nestedType)) {
            yield return nested;
        }
    }
}

static void AddFieldIfMissing(TypeDefinition type, string name, TypeReference fieldType) {
    if (type.Fields.Any(f => f.Name == name)) {
        return;
    }
    type.Fields.Add(new FieldDefinition(name, FieldAttributes.Public | FieldAttributes.Static, fieldType));
}
