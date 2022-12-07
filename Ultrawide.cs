using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.X64;
using Reloaded.Mod.Interfaces;
using System.Diagnostics;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;
using Reloaded.Memory.Sources;
using Register = Reloaded.Hooks.Definitions.X64.FunctionAttribute.Register;

namespace p5rpc.ultrawide
{
    public unsafe class Ultrawide
    {
        [Function(new[] { Register.rdx, Register.rcx, Register.r8 }, Register.rax, false)]
        public delegate void SetResolution(int width, long dest, int height);

        [Function(new[] { Register.rcx, Register.rdx }, Register.rax, false)]
        public delegate void ReadCamera(int p_camera, int _);

        private IHook<SetResolution> _setResolutionHook;
        private IHook<ReadCamera> _cameraHook;
        public Ultrawide(IReloadedHooks hooks, ILogger logger, IModLoader modLoader)
        {
            var thisProcess = Process.GetCurrentProcess();
            long baseAddress = thisProcess.MainModule.BaseAddress.ToInt64();

            var memory = Memory.Instance;
            modLoader.GetController<IStartupScanner>().TryGetTarget(out var startupScanner);

            float defaultAspect = 0;
            var currentAspect = defaultAspect;
            long resolutionPointer = 0;
            int cameraPointer = 0;
            bool isAspectSet = false;

            startupScanner.AddMainModuleScan("F3 0F 59 05 ?? ?? ?? ?? F3 48 ?? ?? ?? 3B ?? 72 ?? 0F 57 ?? F3 48 ?? ?? ?? F3 0F 59 05", (result) =>
            {
                long resolutionStart = result.Offset + baseAddress;
                if (!result.Found)
                    throw new Exception("Signature for getting Resolution not found.");
                resolutionPointer = Util.GetAddressFromGlobalRef(resolutionStart + 25, 8);
                logger.WriteLine($"Resolution is {*(float*)resolutionPointer:0.000} at 0x{resolutionPointer:x}");
            });

            startupScanner.AddMainModuleScan("85 D2 7E ?? 45 85 C0 7E ?? 89 51 1C 44 89 41 20", (result) => 
            {
                var resolutionSetterStart = result.Offset + baseAddress;
                if (!result.Found)
                    throw new Exception("Signature for Resolution Setter not found.");
                void SetResolutionImpl(int width, long dest, int height)
                {
                    currentAspect = (float)width / height;
                    memory.SafeWrite(resolutionPointer, currentAspect);
                    logger.WriteLine($"SetResolution called: {width}x{height}, Resolution is {*(float*)resolutionPointer:0.000}. Function pointer is 0x{baseAddress + result.Offset:x}.");
                    isAspectSet = false;
                    _setResolutionHook.OriginalFunction(width, dest, height);
                }
                _setResolutionHook = hooks.CreateHook<SetResolution>(SetResolutionImpl, resolutionSetterStart).Activate();
            });

            startupScanner.AddMainModuleScan("48 8B C4 44 89 40 18 55 53 56 57 41 56 48 8D", (result) =>
            {
                var readCameraStart = result.Offset + baseAddress;
                if (!result.Found)
                    throw new Exception("Signature for getting Aspect Ratio not found.");
                void ReadCameraImpl(int p_camera, int _)
                {
                    if (!isAspectSet && p_camera != 0)
                    {
                        if (defaultAspect == 0)
                        {
                            cameraPointer = p_camera;
                            defaultAspect = *(float*)(p_camera + 0x1ac);
                            logger.WriteLine($"Default Aspect set to {defaultAspect}");
                        }
                        logger.WriteLine($"Setting Aspect Ratio to {currentAspect}. Camera Pointer is {cameraPointer:x}. Current Aspect is {*(float*)(cameraPointer + 0x1ac)}");
                        memory.SafeWrite(cameraPointer + 0x1ac, currentAspect);
                        isAspectSet = true;
                    }
                    _cameraHook.OriginalFunction(p_camera, _);
                }
                _cameraHook = hooks.CreateHook<ReadCamera>(ReadCameraImpl, readCameraStart).Activate();
            });
        }
    }
}