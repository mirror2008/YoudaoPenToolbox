using System.Collections.Generic;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Models
{
    public class PartitionListResult
    {
        public IReadOnlyList<BlockPartitionInfo> Partitions { get; set; }
        public string ActiveAbSlot { get; set; }
    }
}
