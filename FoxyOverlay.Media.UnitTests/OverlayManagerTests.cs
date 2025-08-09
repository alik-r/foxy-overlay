using System;
using System.IO;
using System.Threading.Tasks;


namespace FoxyOverlay.Media.UnitTests
{
    public class OverlayManagerTests
    {
        [StaFact(Timeout=10000)]
        public async Task PlayAsync_Completes_WithShortDummyVideo()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "foxy.mp4");
            Assert.True(File.Exists(path), "Please copy a short foxy.mp4 alongside the test assembly.");

            var mgr = new OverlayManager();
            await mgr.PlayAsync(path, mute: false);
            
            Assert.True(true);
        }
    }
}