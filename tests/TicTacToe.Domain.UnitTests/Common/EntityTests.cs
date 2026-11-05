using NUnit.Framework;
using Shouldly;
using TicTacToe.Domain.Common;

namespace TicTacToe.Domain.UnitTests.Common;

internal sealed class SampleEntity : Entity<Guid>
{
    public SampleEntity(Guid id) => Id = id;
}

internal sealed class OtherEntity : Entity<Guid>;

public class EntityTests
{
    [Test]
    public void SameIdAndType_AreEqual()
    {
        var id = Guid.NewGuid();
        var a = new SampleEntity(id);
        var b = new SampleEntity(id);

        a.Id.ShouldBe(id);
        a.Equals(b).ShouldBeTrue();
        (a == b).ShouldBeTrue();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Test]
    public void DifferentIdOrType_AreNotEqual()
    {
        var a = new SampleEntity(Guid.NewGuid());
        var b = new SampleEntity(Guid.NewGuid());
        var c = new OtherEntity();

        (a == b).ShouldBeFalse();
        a.Equals(c).ShouldBeFalse();
    }

    [Test]
    public void NullComparisons_Work()
    {
        var a = new SampleEntity(Guid.NewGuid());

        a.Equals(null).ShouldBeFalse();
        (a == null).ShouldBeFalse();
    }
}
