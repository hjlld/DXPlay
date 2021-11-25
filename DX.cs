using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using Vortice.Direct3D12;
using Vortice.Direct3D12.Debug;
using Vortice.DXGI;
using Vortice.Mathematics;
namespace DXPlay
{
    internal struct FrameObject
    {
        public ID3D12CommandAllocator cmdAllocator;
        public ID3D12Resource swapChainBuffer;
        public CpuDescriptorHandle rtvHandle;
    }
    internal struct HeapData
    {
        public ID3D12DescriptorHeap heap;
        public uint usedEntries;
    }
    class DX
    {
        public int frameBufferNum = 2;
        public DX(IntPtr hwnd, int width, int height)
        {

            if (!D3D12.IsSupported(Vortice.Direct3D.FeatureLevel.Level_12_0))
            {
                throw new InvalidOperationException("Direct3D12 is not supported on current OS");
            }
#if DEBUG
            if (D3D12.D3D12GetDebugInterface<ID3D12Debug>(out var pDx12Debug).Success)
            {
                pDx12Debug.EnableDebugLayer();
            }
#endif
            IDXGIFactory4 factory = DXGI.CreateDXGIFactory1<IDXGIFactory4>();

            IDXGIAdapter1 adapter = null;
            
            for (int i = 0; factory.GetAdapter1(i) != null; i++)
            {
                IDXGIAdapter1 _adapter = factory.GetAdapter1(i);

                Debug.WriteLine($@"==== Adapater {i} ====");
                Debug.WriteLine(_adapter.Description.Description);
                Debug.WriteLine(_adapter.Description1.Description);
                Debug.WriteLine($@"======================");

                if (_adapter.Description1.Flags.HasFlag(AdapterFlags.Software))
                {
                    continue;
                } 
                else
                {
                    adapter = _adapter;
                    break;
                }
            }

            if (adapter == null)
            {
                throw new Exception("Hardware adapter not found!");
            }

            ID3D12Device device = D3D12.D3D12CreateDevice<ID3D12Device>(adapter, Vortice.Direct3D.FeatureLevel.Level_12_0);

            //ID3D12DebugDevice debugDevice = device.QueryInterface<ID3D12DebugDevice>();

            CommandQueueDescription cmdQueueDesc = new()
            {
                Flags = CommandQueueFlags.None,
                Type = CommandListType.Direct,
            };

            ID3D12CommandQueue cmdQueue = device.CreateCommandQueue(cmdQueueDesc);

            SwapChainDescription1 swapChainDesc = new()
            {
                BufferCount = frameBufferNum,
                Width = width,
                Height = height,
                Format = Format.R8G8B8A8_UNorm,
                BufferUsage = Usage.RenderTargetOutput,
                SwapEffect = SwapEffect.FlipDiscard,
                SampleDescription = new SampleDescription(1, 0)
            };

            IDXGISwapChain1 swapChain1 = factory.CreateSwapChainForHwnd(cmdQueue, hwnd, swapChainDesc);
            IDXGISwapChain3 swapChain = swapChain1.QueryInterface<IDXGISwapChain3>();

            HeapData rtvHeap = new();

            DescriptorHeapDescription heapDesc = new()
            {
                DescriptorCount = 3,
                Type = DescriptorHeapType.RenderTargetView,
                Flags = DescriptorHeapFlags.None,
            };

            rtvHeap.heap = device.CreateDescriptorHeap(heapDesc);

            var frameObjects = new FrameObject[2];

            for (int i = 0; i < 2; i++)
            {
                frameObjects[i].cmdAllocator = device.CreateCommandAllocator<ID3D12CommandAllocator>(CommandListType.Direct);
                frameObjects[i].swapChainBuffer = swapChain.GetBuffer<ID3D12Resource>(i);

                Texture2DRenderTargetView texture = new()
                {
                    MipSlice = 0
                };

                RenderTargetViewDescription desc = new()
                {
                    ViewDimension = RenderTargetViewDimension.Texture2D,
                    Format = Format.R8G8B8A8_UNorm_SRgb,
                    Texture2D = texture,
                };
                CpuDescriptorHandle rtvHandle = rtvHeap.heap.GetCPUDescriptorHandleForHeapStart();
                rtvHandle.Ptr += rtvHeap.usedEntries * device.GetDescriptorHandleIncrementSize(DescriptorHeapType.RenderTargetView);
                rtvHeap.usedEntries++;
                device.CreateRenderTargetView(frameObjects[i].swapChainBuffer, desc, rtvHandle);
                frameObjects[i].rtvHandle = rtvHandle;
            }

            ID3D12GraphicsCommandList4 cmdList = device.CreateCommandList<ID3D12GraphicsCommandList4>(0, CommandListType.Direct, frameObjects[0].cmdAllocator, null);

            ID3D12Fence fence = device.CreateFence(0, FenceFlags.None);
            var fenceEvent = new EventWaitHandle(false, EventResetMode.AutoReset);

            Color4 clearColor = new(1, 0, 0, 1);
            Vortice.RawRect swapChainRect = new(0, 0, width, height);

            //Per frame
            int rtvIndex = swapChain.CurrentBackBufferIndex;
            ResourceBarrier barrier = new(new ResourceTransitionBarrier(frameObjects[rtvIndex].swapChainBuffer, ResourceStates.Present, ResourceStates.RenderTarget));
            cmdList.ResourceBarrier(barrier);
            cmdList.ClearRenderTargetView(frameObjects[rtvIndex].rtvHandle, clearColor);

            barrier = new(new ResourceTransitionBarrier(frameObjects[rtvIndex].swapChainBuffer, ResourceStates.RenderTarget, ResourceStates.Present));
            cmdList.ResourceBarrier(barrier);

            uint fenceValue = 0;

            cmdList.Close();
            cmdQueue.ExecuteCommandList(cmdList);
            fenceValue++;
            cmdQueue.Signal(fence, fenceValue);

            swapChain.Present(0, 0);

            //int bufferIndex = swapChain.CurrentBackBufferIndex;


        }
    }
}
