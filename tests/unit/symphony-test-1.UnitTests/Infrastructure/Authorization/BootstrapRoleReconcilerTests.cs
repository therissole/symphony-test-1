using SymphonyTest1.OpenFgaProvisioning;

namespace SymphonyTest1.UnitTests.Infrastructure.Authorization;

[TestFixture]
public sealed class BootstrapRoleReconcilerTests
{
    [Test]
    public void CreatePlan_AddsMissingAndRemovesStaleManagedRoleTuples()
    {
        var desired = BootstrapRoleReconciler.CreateDesiredTuples(
            ["superuser-subject"],
            ["standard-subject"]);
        BootstrapRoleTuple[] current =
        [
            new("user:superuser-subject", "superuser", "system:global"),
            new("user:stale-subject", "standard_user", "system:global")
        ];

        var plan = BootstrapRoleReconciler.CreatePlan(current, desired);

        Assert.Multiple(() =>
        {
            Assert.That(
                plan.Writes,
                Is.EqualTo(new[]
                {
                    new BootstrapRoleTuple(
                        "user:standard-subject",
                        "standard_user",
                        "system:global")
                }));
            Assert.That(
                plan.Deletes,
                Is.EqualTo(new[]
                {
                    new BootstrapRoleTuple(
                        "user:stale-subject",
                        "standard_user",
                        "system:global")
                }));
        });
    }

    [Test]
    public void CreatePlan_EmptyConfigurationRevokesAllManagedRolesWithoutTouchingResourceTuples()
    {
        var desired = BootstrapRoleReconciler.CreateDesiredTuples([], []);
        BootstrapRoleTuple[] current =
        [
            new("user:stale-superuser", "superuser", "system:global"),
            new("user:stale-standard-user", "standard_user", "system:global"),
            new("system:global", "system", "language:2a8a203a-0fcc-410c-af65-d37716469bb4"),
            new("system:global", "system", "greeting:a05f5993-540c-4202-b761-383ed37e0daf")
        ];

        var plan = BootstrapRoleReconciler.CreatePlan(current, desired);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Writes, Is.Empty);
            Assert.That(
                plan.Deletes,
                Is.EqualTo(new[]
                {
                    new BootstrapRoleTuple(
                        "user:stale-standard-user",
                        "standard_user",
                        "system:global"),
                    new BootstrapRoleTuple(
                        "user:stale-superuser",
                        "superuser",
                        "system:global")
                }));
        });
    }

    [Test]
    public void CreateDesiredTuples_DeduplicatesConfiguredSubjectsPerRole()
    {
        var desired = BootstrapRoleReconciler.CreateDesiredTuples(
            ["shared-subject", "shared-subject"],
            ["shared-subject", "shared-subject"]);

        Assert.That(desired, Has.Count.EqualTo(2));
    }
}
