namespace FolderSync.Sync.Comparison;

public interface IFileComparer
{
    bool AreEqual(FileInfo source, FileInfo replica);
}
