//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System.Collections.Generic;

namespace FoxSsh.Common.Compression
{
    public class CompressionNone : CompressionAlgorithm
    {
        public override IEnumerable<byte> Compress(IEnumerable<byte> input)
        {
            return input;
        }

        public override IEnumerable<byte> Decompress(IEnumerable<byte> input)
        {
            return input;
        }
    }
}
