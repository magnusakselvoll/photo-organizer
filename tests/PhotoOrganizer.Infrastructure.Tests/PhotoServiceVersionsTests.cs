using PhotoOrganizer.Application.Photos;
using PhotoOrganizer.Domain;
using PhotoOrganizer.Domain.Interfaces;
using PhotoOrganizer.Infrastructure.Services;

namespace PhotoOrganizer.Infrastructure.Tests;

/// <summary>
/// Tests that PhotoService.GetPhotoByIdAsync populates the Versions list for photos
/// that belong to a duplicate group, and leaves it empty for standalone photos.
/// </summary>
[TestClass]
public sealed class PhotoServiceVersionsTests
{
    private static readonly Guid GroupId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private static Photo MakePhoto(string path, bool isPreferred = false, Guid? groupId = null,
        FolderType folderType = FolderType.Originals) => new()
    {
        Id = Guid.NewGuid(),
        FilePath = path,
        FileName = Path.GetFileName(path),
        FolderType = folderType,
        IsPreferred = isPreferred,
        DuplicateGroupId = groupId,
    };

    [TestMethod]
    public async Task GetPhotoByIdAsync_StandalonePhoto_VersionsIsEmpty()
    {
        var photo = MakePhoto("/photos/standalone.jpg");
        var service = new PhotoService(new StubRepo([photo]));

        var result = await service.GetPhotoByIdAsync(photo.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Versions.Count);
    }

    [TestMethod]
    public async Task GetPhotoByIdAsync_GroupPhoto_VersionsListsAllSiblings()
    {
        var preferred = MakePhoto("/edits/photo.jpg", isPreferred: true, groupId: GroupId, folderType: FolderType.Edits);
        var original  = MakePhoto("/originals/photo.jpg", isPreferred: false, groupId: GroupId, folderType: FolderType.Originals);
        var raw       = MakePhoto("/originals/photo.orf", isPreferred: false, groupId: GroupId, folderType: FolderType.Originals);

        var service = new PhotoService(new StubRepo([preferred, original, raw]));

        var result = await service.GetPhotoByIdAsync(original.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Versions.Count, "All three siblings should be listed");
    }

    [TestMethod]
    public async Task GetPhotoByIdAsync_VersionsOrderedPreferredFirst()
    {
        var preferred = MakePhoto("/edits/photo.jpg", isPreferred: true, groupId: GroupId, folderType: FolderType.Edits);
        var nonPref   = MakePhoto("/originals/photo.jpg", isPreferred: false, groupId: GroupId);

        var service = new PhotoService(new StubRepo([nonPref, preferred]));

        var result = await service.GetPhotoByIdAsync(nonPref.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Versions.Count);
        Assert.IsTrue(result.Versions[0].IsPreferred, "Preferred version should be listed first");
        Assert.IsFalse(result.Versions[1].IsPreferred);
    }

    [TestMethod]
    public async Task GetPhotoByIdAsync_VersionsContainCorrectFileNames()
    {
        var preferred = MakePhoto("/edits/photo.jpg", isPreferred: true, groupId: GroupId, folderType: FolderType.Edits);
        var original  = MakePhoto("/originals/photo.jpg", isPreferred: false, groupId: GroupId, folderType: FolderType.Originals);

        var service = new PhotoService(new StubRepo([preferred, original]));

        var result = await service.GetPhotoByIdAsync(original.Id);

        Assert.IsNotNull(result);
        var fileNames = result.Versions.Select(v => v.FileName).ToHashSet();
        Assert.IsTrue(fileNames.Contains("photo.jpg"), "Both files share the same name in this fixture");
        var folderTypes = result.Versions.Select(v => v.FolderType).ToHashSet();
        Assert.IsTrue(folderTypes.Contains("Edits"));
        Assert.IsTrue(folderTypes.Contains("Originals"));
    }

    [TestMethod]
    public async Task GetPhotoByIdAsync_OnlyOwnGroupSiblings_NotOtherGroups()
    {
        var groupA1 = MakePhoto("/photos/a.jpg", isPreferred: true, groupId: GroupId);
        var groupA2 = MakePhoto("/photos/a_edit.jpg", isPreferred: false, groupId: GroupId);
        var groupB  = MakePhoto("/photos/b.jpg", isPreferred: true, groupId: Guid.NewGuid());

        var service = new PhotoService(new StubRepo([groupA1, groupA2, groupB]));

        var result = await service.GetPhotoByIdAsync(groupA1.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Versions.Count, "Should only list group-A siblings, not group-B");
        Assert.IsTrue(result.Versions.All(v => v.Id != groupB.Id));
    }

    private sealed class StubRepo(IEnumerable<Photo> photos) : IPhotoRepository
    {
        private readonly IReadOnlyList<Photo> _photos = photos.ToList();

        public Task<IReadOnlyList<Photo>> GetAllPhotosAsync() => Task.FromResult(_photos);
        public Task<Photo?> GetByIdAsync(Guid id) => Task.FromResult(_photos.FirstOrDefault(p => p.Id == id));
    }
}
