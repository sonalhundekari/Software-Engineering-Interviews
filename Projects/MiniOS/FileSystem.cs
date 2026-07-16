namespace MiniOS;

/// <summary>
/// A file's metadata: which inode number it is, and which disk blocks
/// hold its data, in order. Compare to xv6's struct dinode in fs.h --
/// a real inode also tracks type, permissions, link count, and a mix
/// of direct/indirect block pointers; this keeps only direct pointers.
/// </summary>
public class Inode
{
    public int Number { get; }
    public List<int> BlockNumbers { get; } = new();
    public int SizeBytes { get; set; }

    public Inode(int number) => Number = number;
}

/// <summary>
/// A minimal inode-based filesystem living entirely in memory: a fixed
/// number of fixed-size blocks, a free-block bitmap, an inode table, and
/// a flat directory mapping names to inode numbers (compare to xv6's
/// kernel/fs.c). There is no journal here, so this intentionally does
/// not demonstrate crash consistency -- only the allocation and lookup
/// structures.
/// </summary>
public class SimpleFileSystem
{
    private const int BlockSize = 16; // bytes per block, kept tiny so allocation is visible across a few blocks
    private readonly string?[] _blocks;
    private readonly bool[] _blockUsed;
    private readonly Dictionary<int, Inode> _inodeTable = new();
    private readonly Dictionary<string, int> _directory = new();
    private int _nextInodeNumber = 1;
    private readonly Action<string> _log;

    public SimpleFileSystem(Action<string> log, int totalBlocks = 32)
    {
        _log = log;
        _blocks = new string?[totalBlocks];
        _blockUsed = new bool[totalBlocks];
    }

    public void Create(string name)
    {
        if (_directory.ContainsKey(name))
        {
            _log($"  [fs]         create '{name}' skipped: already exists");
            return;
        }

        var inode = new Inode(_nextInodeNumber++);
        _inodeTable[inode.Number] = inode;
        _directory[name] = inode.Number;
        _log($"  [fs]         created '{name}' -> inode {inode.Number}");
    }

    public void Write(string name, string data)
    {
        if (!_directory.TryGetValue(name, out var inodeNumber))
        {
            Create(name);
            inodeNumber = _directory[name];
        }

        var inode = _inodeTable[inodeNumber];

        // Free the file's existing blocks before rewriting, same as
        // truncating a file before a full overwrite.
        foreach (var b in inode.BlockNumbers)
        {
            _blocks[b] = null;
            _blockUsed[b] = false;
        }
        inode.BlockNumbers.Clear();

        for (int offset = 0; offset < data.Length; offset += BlockSize)
        {
            var chunk = data.Substring(offset, Math.Min(BlockSize, data.Length - offset));
            var block = AllocateBlock();
            _blocks[block] = chunk;
            inode.BlockNumbers.Add(block);
            _log($"  [fs]         wrote block {block} for inode {inode.Number}: \"{chunk}\"");
        }

        inode.SizeBytes = data.Length;
    }

    public string Read(string name)
    {
        if (!_directory.TryGetValue(name, out var inodeNumber))
        {
            _log($"  [fs]         read '{name}' failed: no such file");
            return string.Empty;
        }

        var inode = _inodeTable[inodeNumber];
        var content = string.Concat(inode.BlockNumbers.Select(b => _blocks[b]));
        _log($"  [fs]         read '{name}' ({inode.SizeBytes} bytes across {inode.BlockNumbers.Count} block(s))");
        return content;
    }

    public void Delete(string name)
    {
        if (!_directory.TryGetValue(name, out var inodeNumber)) return;

        var inode = _inodeTable[inodeNumber];
        foreach (var b in inode.BlockNumbers)
        {
            _blocks[b] = null;
            _blockUsed[b] = false;
        }
        _inodeTable.Remove(inodeNumber);
        _directory.Remove(name);
        _log($"  [fs]         deleted '{name}' (inode {inodeNumber} and its blocks freed)");
    }

    private int AllocateBlock()
    {
        for (int i = 0; i < _blockUsed.Length; i++)
        {
            if (!_blockUsed[i])
            {
                _blockUsed[i] = true;
                return i;
            }
        }
        throw new InvalidOperationException("MiniOS: filesystem out of free blocks");
    }
}
