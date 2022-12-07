using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.X64;
using Reloaded.Mod.Interfaces;
using System.Diagnostics;
using Reloaded.Memory.Sigscan;
using Reloaded.Memory.Sources;
using Register = Reloaded.Hooks.Definitions.X64.FunctionAttribute.Register;

namespace p5rpc.ultrawide
{
    public unsafe class Ultrawide
    {
        [Function(new[] { Register.rdx, Register.rcx, Register.r8 }, Register.rax, false)]
        public delegate void SetResolution(int width, long dest, int height);

        [Function(new[] { Register.rcx, Register.rdx }, Register.rax, true)]
        public delegate void ReadCamera(int p_camera, int _);

        private IHook<SetResolution> _setResolutionHook;
        private IHook<ReadCamera> _cameraHook;
        public Ultrawide(IReloadedHooks hooks, ILogger logger, IModLoader modLoader)
        {
            var thisProcess = Process.GetCurrentProcess();
            var baseAddress = thisProcess.MainModule.BaseAddress;
            var exeSize = thisProcess.MainModule.ModuleMemorySize;
            float defaultAspect = 0;
            var currentAspect = defaultAspect;
            var memory = Memory.Instance;
            using var scanner = new Scanner((byte*)baseAddress, exeSize);

            var resolutionResult = scanner.FindPattern("F3 0F 59 05 ?? ?? ?? ?? F3 48 ?? ?? ?? 3B ?? 72 ?? 0F 57 ?? F3 48 ?? ?? ?? F3 0F 59 05");
            if (!resolutionResult.Found)
                throw new Exception("Signature for getting resolution not found.");

            var resolutionCodeAddress = baseAddress + resolutionResult.Offset;
            var resolutionPointer = Util.GetAddressFromGlobalRef(resolutionCodeAddress + 25, 8);

            var setResolutionResult = scanner.FindPattern("85 D2 7E ?? 45 85 C0 7E ?? 89 51 1C 44 89 41 20");
            if (!setResolutionResult.Found)
                throw new Exception("Resolution Setter not found.");

            int cameraPointer = 0;
            bool isAspectSet = false;

            var aspectRatioResult = scanner.FindPattern("48 8B C4 44 89 40 18 55 53 56 57 41 56 48 8D");
            if (!aspectRatioResult.Found)
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
            _cameraHook = hooks.CreateHook<ReadCamera>(ReadCameraImpl, (baseAddress + aspectRatioResult.Offset)).Activate();


            void SetResolutionImpl(int width, long dest, int height)
            {
                currentAspect = (float)width / height;
                memory.SafeWrite(resolutionPointer, currentAspect);
                logger.WriteLine($"SetResolution called: {width}x{height}, Resolution is {*(float*)resolutionPointer:0.000}. Function pointer is 0x{baseAddress + setResolutionResult.Offset:x}.");
                isAspectSet = false;
                _setResolutionHook.OriginalFunction(width, dest, height);
            }
            _setResolutionHook = hooks.CreateHook<SetResolution>(SetResolutionImpl, (baseAddress + setResolutionResult.Offset)).Activate();

            logger.WriteLine($"Resolution is {*(float*)resolutionPointer:0.000} at 0x{resolutionPointer:x}");
        }
    }
}