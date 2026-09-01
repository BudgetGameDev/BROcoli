using System.Text;
using NUnit.Framework;

namespace BudgetGameDev.Shared.Tests
{
    public sealed class DisplayEdidMetadataTests
    {
        [Test]
        public void CtaHdrStaticMetadataDecodesLuminanceAndDisplayName()
        {
            byte[] edid = CreateEdid(maximumCode: 64, fullFrameCode: 32, minimumCode: 255);

            bool parsed = DisplayEdidMetadata.TryParse(edid, out DisplayEdidMetadata metadata);

            Assert.That(parsed, Is.True);
            Assert.That(metadata.HasHdrStaticMetadata, Is.True);
            Assert.That(metadata.DisplayName, Is.EqualTo("TEST HDR"));
            Assert.That(metadata.HasMaximumLuminance, Is.True);
            Assert.That(metadata.MaximumLuminanceNits, Is.EqualTo(200f).Within(0.01f));
            Assert.That(metadata.MaximumFullFrameLuminanceNits, Is.EqualTo(100f).Within(0.01f));
            Assert.That(metadata.MinimumLuminanceNits, Is.EqualTo(2f).Within(0.01f));
        }

        [Test]
        public void ZeroCtaLuminanceCodesAreReportedAsUnspecified()
        {
            byte[] edid = CreateEdid(maximumCode: 0, fullFrameCode: 0, minimumCode: 0);

            bool parsed = DisplayEdidMetadata.TryParse(edid, out DisplayEdidMetadata metadata);

            Assert.That(parsed, Is.True);
            Assert.That(metadata.HasHdrStaticMetadata, Is.True);
            Assert.That(metadata.HasMaximumLuminance, Is.False);
            Assert.That(metadata.HasMaximumFullFrameLuminance, Is.False);
            Assert.That(metadata.HasMinimumLuminance, Is.False);
        }

        [Test]
        public void InvalidEdidDoesNotProduceDisplayCapabilities()
        {
            Assert.That(
                DisplayEdidMetadata.TryParse(new byte[256], out DisplayEdidMetadata metadata),
                Is.False
            );
            Assert.That(metadata.HasHdrStaticMetadata, Is.False);
        }

        private static byte[] CreateEdid(
            byte maximumCode,
            byte fullFrameCode,
            byte minimumCode
        )
        {
            byte[] edid = new byte[256];
            byte[] header = { 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x00 };
            header.CopyTo(edid, 0);
            edid[126] = 1;
            byte[] name = Encoding.ASCII.GetBytes("TEST HDR    \n");
            edid[54 + 3] = 0xfc;
            name.CopyTo(edid, 54 + 5);

            const int extension = 128;
            edid[extension] = 0x02;
            edid[extension + 1] = 0x03;
            edid[extension + 2] = 11;
            edid[extension + 4] = (7 << 5) | 6;
            edid[extension + 5] = 0x06;
            edid[extension + 6] = 0x04;
            edid[extension + 7] = 0x01;
            edid[extension + 8] = maximumCode;
            edid[extension + 9] = fullFrameCode;
            edid[extension + 10] = minimumCode;
            return edid;
        }
    }
}
