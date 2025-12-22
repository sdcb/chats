using System;
using System.Collections.Generic;
using System.Linq;
using Chats.Web.Infrastructure;
using Xunit;

namespace Chats.Web.Tests.Common;

public class ReorderHelperTest
{
    // 测试用的实体类
    private class TestOrderableEntity : IOrderable
    {
        public short Order { get; set; }
        public string Name { get; set; } = "";
    }

    private class TestUpdatableEntity : IOrderable, IUpdatable
    {
        public short Order { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string Name { get; set; } = "";
    }

    #region ReorderHelper Constructor Tests

    [Fact]
    public void Constructor_WithValidOptions_ShouldCreateInstance()
    {
        // Arrange
        ReorderOptions options = new ReorderOptions
        {
            ReorderSteps = [1000, 100, 10],
            ReorderStart = -30000,
            MoveStep = 1000
        };

        // Act
        ReorderHelper helper = new ReorderHelper(options);

        // Assert
        Assert.NotNull(helper);
        Assert.Equal(1000, helper.MoveStep);
        Assert.Equal(-30000, helper.ReorderStart);
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ReorderHelper(null!));
    }

    [Fact]
    public void Constructor_WithNullReorderSteps_ShouldThrowArgumentException()
    {
        // Arrange
        ReorderOptions options = new ReorderOptions
        {
            ReorderSteps = null!,
            ReorderStart = -30000,
            MoveStep = 1000
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new ReorderHelper(options));
    }

    [Fact]
    public void Constructor_WithEmptyReorderSteps_ShouldThrowArgumentException()
    {
        // Arrange
        ReorderOptions options = new ReorderOptions
        {
            ReorderSteps = [],
            ReorderStart = -30000,
            MoveStep = 1000
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new ReorderHelper(options));
    }

    [Fact]
    public void Default_ShouldHaveCorrectValues()
    {
        // Act
        ReorderHelper defaultHelper = ReorderHelper.Default;

        // Assert
        Assert.NotNull(defaultHelper);
        Assert.Equal(1000, defaultHelper.MoveStep);
        Assert.Equal(-30000, defaultHelper.ReorderStart);
    }

    #endregion

    #region ReorderEntities Tests

    [Fact]
    public void ReorderEntities_WithEmptyArray_ShouldNotThrow()
    {
        // Arrange
        TestOrderableEntity[] entities = Array.Empty<TestOrderableEntity>();

        // Act & Assert (should not throw)
        ReorderHelper.Default.ReorderEntities(entities);
    }

    [Fact]
    public void ReorderEntities_WithSingleEntity_ShouldSetCorrectOrder()
    {
        // Arrange
        TestOrderableEntity[] entities = new[] { new TestOrderableEntity { Name = "Entity1" } };

        // Act
        ReorderHelper.Default.ReorderEntities(entities);

        // Assert
        Assert.Equal(-30000, entities[0].Order); // actualStart + 0 * actualStep
    }

    [Fact]
    public void ReorderEntities_WithMultipleEntities_ShouldUsePreferredStep()
    {
        // Arrange
        TestOrderableEntity[] entities = new[]
        {
            new TestOrderableEntity { Name = "Entity1" },
            new TestOrderableEntity { Name = "Entity2" },
            new TestOrderableEntity { Name = "Entity3" }
        };

        // Act
        ReorderHelper.Default.ReorderEntities(entities);

        // Assert
        Assert.Equal(-30000, entities[0].Order); // -30000 + 0*1000
        Assert.Equal(-29000, entities[1].Order); // -30000 + 1*1000
        Assert.Equal(-28000, entities[2].Order); // -30000 + 2*1000
    }

    [Fact]
    public void ReorderEntities_WhenPreferredStepCausesOverflow_ShouldUseSmallerStep()
    {
        // Arrange - 创建一个会导致1000步长溢出的配置
        ReorderOptions options = new ReorderOptions
        {
            ReorderSteps = [30000, 100, 10], // 第一个步长会溢出
            ReorderStart = 0,
            MoveStep = 1000
        };
        ReorderHelper helper = new ReorderHelper(options);
        TestOrderableEntity[] entities = new[]
        {
            new TestOrderableEntity { Name = "Entity1" },
            new TestOrderableEntity { Name = "Entity2" },
            new TestOrderableEntity { Name = "Entity3" }
        };

        // Act
        helper.ReorderEntities(entities);

        // Assert - 应该使用100的步长
        Assert.Equal(0, entities[0].Order);   // 0 + 0*100
        Assert.Equal(100, entities[1].Order); // 0 + 1*100
        Assert.Equal(200, entities[2].Order); // 0 + 2*100
    }

    [Fact]
    public void ReorderEntities_WhenAllStepsCauseOverflow_ShouldUseAverageDistribution()
    {
        // Arrange - 创建一个所有步长都会溢出的配置
        ReorderOptions options = new ReorderOptions
        {
            ReorderSteps = [30000, 20000, 10000],
            ReorderStart = 30000,
            MoveStep = 1000
        };
        ReorderHelper helper = new ReorderHelper(options);
        TestOrderableEntity[] entities = new[]
        {
            new TestOrderableEntity { Name = "Entity1" },
            new TestOrderableEntity { Name = "Entity2" }
        };

        // Act
        helper.ReorderEntities(entities);

        // Assert - 应该使用平均分布
        Assert.True(entities[0].Order < entities[1].Order);
        Assert.True(entities[0].Order >= short.MinValue);
        Assert.True(entities[1].Order <= short.MaxValue);
    }

    [Fact]
    public void ReorderEntities_WhenStartPositionNeedsAdjustment_ShouldAdjustStartPosition()
    {
        // Arrange - 配置从接近最大值开始，但步长较小可以通过调整起始位置来适配
        ReorderOptions options = new ReorderOptions
        {
            ReorderSteps = [1000, 100, 10],
            ReorderStart = 32000, // 接近 short.MaxValue
            MoveStep = 1000
        };
        ReorderHelper helper = new ReorderHelper(options);
        TestOrderableEntity[] entities = new[]
        {
            new TestOrderableEntity { Name = "Entity1" },
            new TestOrderableEntity { Name = "Entity2" }
        };

        // Act
        helper.ReorderEntities(entities);

        // Assert - 应该成功设置Order值，且不会溢出
        Assert.True(entities[0].Order <= short.MaxValue);
        Assert.True(entities[1].Order <= short.MaxValue);
        Assert.True(entities[0].Order < entities[1].Order);
    }

    [Fact]
    public void ReorderEntities_WithTooManyEntities_ShouldThrowInvalidOperationException()
    {
        // Arrange - 创建超过 short 范围的实体数量
        TestOrderableEntity[] entities = new TestOrderableEntity[70000]; // 超过 65536
        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = new TestOrderableEntity { Name = $"Entity{i}" };
        }

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => ReorderHelper.Default.ReorderEntities(entities));
    }

    [Fact]
    public void ReorderEntities_WithCustomSteps_ShouldUseCustomConfiguration()
    {
        // Arrange
        ReorderOptions options = new ReorderOptions
        {
            ReorderSteps = [500, 50, 5],
            ReorderStart = -15000,
            MoveStep = 500
        };
        ReorderHelper helper = new ReorderHelper(options);
        TestOrderableEntity[] entities = new[]
        {
            new TestOrderableEntity { Name = "Entity1" },
            new TestOrderableEntity { Name = "Entity2" }
        };

        // Act
        helper.ReorderEntities(entities);

        // Assert
        Assert.Equal(-15000, entities[0].Order); // -15000 + 0*500
        Assert.Equal(-14500, entities[1].Order); // -15000 + 1*500
    }

    #endregion

    #region TryApplyMove Tests

    [Fact]
    public void TryApplyMove_BetweenTwoEntities_WithEnoughSpace_ShouldReturnTrueAndSetMiddleValue()
    {
        // Arrange
        TestOrderableEntity previous = new TestOrderableEntity { Order = 1000 };
        TestOrderableEntity next = new TestOrderableEntity { Order = 2000 };
        TestOrderableEntity source = new TestOrderableEntity { Order = 500 };

        // Act
        bool result = ReorderHelper.Default.TryApplyMove(source, previous, next);

        // Assert
        Assert.True(result);
        Assert.Equal(1500, source.Order); // (1000 + 2000) / 2
    }

    [Fact]
    public void TryApplyMove_BetweenTwoEntities_WithInsufficientSpace_ShouldReturnFalse()
    {
        // Arrange
        TestOrderableEntity previous = new TestOrderableEntity { Order = 1000 };
        TestOrderableEntity next = new TestOrderableEntity { Order = 1001 }; // 只有1的间隔
        TestOrderableEntity source = new TestOrderableEntity { Order = 500 };

        // Act
        bool result = ReorderHelper.Default.TryApplyMove(source, previous, next);

        // Assert
        Assert.False(result);
        Assert.Equal(500, source.Order); // Order 应该保持不变
    }

    [Fact]
    public void TryApplyMove_AfterPrevious_WithinRange_ShouldReturnTrueAndAddMoveStep()
    {
        // Arrange
        TestOrderableEntity previous = new TestOrderableEntity { Order = 1000 };
        TestOrderableEntity source = new TestOrderableEntity { Order = 500 };

        // Act
        bool result = ReorderHelper.Default.TryApplyMove(source, previous, null);

        // Assert
        Assert.True(result);
        Assert.Equal(2000, source.Order); // 1000 + 1000 (MoveStep)
    }

    [Fact]
    public void TryApplyMove_AfterPrevious_CausingOverflow_ShouldReturnFalse()
    {
        // Arrange
        TestOrderableEntity previous = new TestOrderableEntity { Order = short.MaxValue };
        TestOrderableEntity source = new TestOrderableEntity { Order = 500 };

        // Act
        bool result = ReorderHelper.Default.TryApplyMove(source, previous, null);

        // Assert
        Assert.False(result);
        Assert.Equal(500, source.Order); // Order 应该保持不变
    }

    [Fact]
    public void TryApplyMove_BeforeNext_WithinRange_ShouldReturnTrueAndSubtractMoveStep()
    {
        // Arrange
        TestOrderableEntity next = new TestOrderableEntity { Order = 1000 };
        TestOrderableEntity source = new TestOrderableEntity { Order = 500 };

        // Act
        bool result = ReorderHelper.Default.TryApplyMove(source, null, next);

        // Assert
        Assert.True(result);
        Assert.Equal(0, source.Order); // 1000 - 1000 (MoveStep)
    }

    [Fact]
    public void TryApplyMove_BeforeNext_CausingUnderflow_ShouldReturnFalse()
    {
        // Arrange
        TestOrderableEntity next = new TestOrderableEntity { Order = short.MinValue };
        TestOrderableEntity source = new TestOrderableEntity { Order = 500 };

        // Act
        bool result = ReorderHelper.Default.TryApplyMove(source, null, next);

        // Assert
        Assert.False(result);
        Assert.Equal(500, source.Order); // Order 应该保持不变
    }

    [Fact]
    public void TryApplyMove_WithCustomMoveStep_ShouldUseCustomStep()
    {
        // Arrange
        ReorderOptions options = new ReorderOptions
        {
            ReorderSteps = [1000, 100, 10],
            ReorderStart = -30000,
            MoveStep = 500 // 自定义移动步长
        };
        ReorderHelper helper = new ReorderHelper(options);
        TestOrderableEntity previous = new TestOrderableEntity { Order = 1000 };
        TestOrderableEntity source = new TestOrderableEntity { Order = 100 };

        // Act
        bool result = helper.TryApplyMove(source, previous, null);

        // Assert
        Assert.True(result);
        Assert.Equal(1500, source.Order); // 1000 + 500 (自定义 MoveStep)
    }

    [Fact]
    public void TryApplyMove_BetweenAdjacentEntities_ShouldReturnFalse()
    {
        // Arrange
        TestOrderableEntity previous = new TestOrderableEntity { Order = 1000 };
        TestOrderableEntity next = new TestOrderableEntity { Order = 1000 }; // 相同值
        TestOrderableEntity source = new TestOrderableEntity { Order = 500 };

        // Act
        bool result = ReorderHelper.Default.TryApplyMove(source, previous, next);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region RedistributeInRange Tests

    [Fact]
    public void RedistributeInRange_WithEmptyArray_ShouldNotThrow()
    {
        // Arrange
        TestUpdatableEntity[] entities = Array.Empty<TestUpdatableEntity>();

        // Act & Assert (should not throw)
        ReorderHelper.Default.RedistributeInRange(entities, -1000, 1000);
    }

    [Fact]
    public void RedistributeInRange_WithSufficientSpace_ShouldDistributeEvenly()
    {
        // Arrange
        TestUpdatableEntity[] entities = new[]
        {
            new TestUpdatableEntity { Name = "Entity1", UpdatedAt = DateTime.MinValue },
            new TestUpdatableEntity { Name = "Entity2", UpdatedAt = DateTime.MinValue },
            new TestUpdatableEntity { Name = "Entity3", UpdatedAt = DateTime.MinValue }
        };

        // Act
        ReorderHelper.Default.RedistributeInRange(entities, 0, 1000);

        // Assert
        Assert.True(entities[0].Order < entities[1].Order);
        Assert.True(entities[1].Order < entities[2].Order);
        Assert.True(entities[0].Order >= 0);
        Assert.True(entities[2].Order <= 1000);
        
        // 验证 UpdatedAt 被设置
        Assert.True(entities[0].UpdatedAt > DateTime.MinValue);
        Assert.True(entities[1].UpdatedAt > DateTime.MinValue);
        Assert.True(entities[2].UpdatedAt > DateTime.MinValue);
    }

    [Fact]
    public void RedistributeInRange_WithInsufficientSpace_ShouldDistributeAsPossible()
    {
        // Arrange
        TestUpdatableEntity[] entities = new[]
        {
            new TestUpdatableEntity { Name = "Entity1" },
            new TestUpdatableEntity { Name = "Entity2" },
            new TestUpdatableEntity { Name = "Entity3" },
            new TestUpdatableEntity { Name = "Entity4" },
            new TestUpdatableEntity { Name = "Entity5" }
        };

        // Act - 只有3个单位的空间，但有5个实体
        ReorderHelper.Default.RedistributeInRange(entities, 0, 2);

        // Assert
        Assert.True(entities[0].Order >= 0);
        Assert.True(entities[4].Order <= 2);
        
        // 验证顺序仍然保持
        for (int i = 0; i < entities.Length - 1; i++)
        {
            Assert.True(entities[i].Order <= entities[i + 1].Order);
        }
    }

    [Fact]
    public void RedistributeInRange_WithSingleEntity_ShouldSetToRangeStart()
    {
        // Arrange
        TestUpdatableEntity entity = new TestUpdatableEntity { Name = "Entity1", UpdatedAt = DateTime.MinValue };
        TestUpdatableEntity[] entities = new[] { entity };

        // Act
        ReorderHelper.Default.RedistributeInRange(entities, 100, 200);

        // Assert
        Assert.True(entity.Order >= 100);
        Assert.True(entity.Order <= 200);
        Assert.True(entity.UpdatedAt > DateTime.MinValue);
    }

    [Fact]
    public void RedistributeInRange_WithNegativeRange_ShouldWork()
    {
        // Arrange
        TestUpdatableEntity[] entities = new[]
        {
            new TestUpdatableEntity { Name = "Entity1" },
            new TestUpdatableEntity { Name = "Entity2" }
        };

        // Act
        ReorderHelper.Default.RedistributeInRange(entities, -1000, -500);

        // Assert
        Assert.True(entities[0].Order >= -1000);
        Assert.True(entities[1].Order <= -500);
        Assert.True(entities[0].Order < entities[1].Order);
    }

    [Fact]
    public void RedistributeInRange_WithMaxRange_ShouldWork()
    {
        // Arrange
        TestUpdatableEntity[] entities = new[]
        {
            new TestUpdatableEntity { Name = "Entity1" },
            new TestUpdatableEntity { Name = "Entity2" }
        };

        // Act
        ReorderHelper.Default.RedistributeInRange(entities, short.MinValue, short.MaxValue);

        // Assert
        Assert.True(entities[0].Order >= short.MinValue);
        Assert.True(entities[1].Order <= short.MaxValue);
        Assert.True(entities[0].Order < entities[1].Order);
    }

    [Fact]
    public void RedistributeInRange_WithSameMinMax_ShouldSetAllToSameValue()
    {
        // Arrange
        TestUpdatableEntity[] entities = new[]
        {
            new TestUpdatableEntity { Name = "Entity1" },
            new TestUpdatableEntity { Name = "Entity2" },
            new TestUpdatableEntity { Name = "Entity3" }
        };

        // Act
        ReorderHelper.Default.RedistributeInRange(entities, 100, 100);

        // Assert
        Assert.All(entities, entity => Assert.Equal(100, entity.Order));
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Integration_ReorderThenTryMove_ShouldWork()
    {
        // Arrange
        TestOrderableEntity[] entities = new[]
        {
            new TestOrderableEntity { Name = "Entity1" },
            new TestOrderableEntity { Name = "Entity2" },
            new TestOrderableEntity { Name = "Entity3" }
        };

        // Act - 先重排序
        ReorderHelper.Default.ReorderEntities(entities);

        // 然后尝试移动第一个实体到第二和第三个之间
        TestOrderableEntity moved = entities[0];
        bool moveResult = ReorderHelper.Default.TryApplyMove(moved, entities[1], entities[2]);

        // Assert
        Assert.True(moveResult);
        Assert.True(moved.Order > entities[1].Order);
        Assert.True(moved.Order < entities[2].Order);
    }

    [Fact]
    public void Integration_ReorderThenRedistribute_ShouldWork()
    {
        // Arrange
        TestUpdatableEntity[] entities = new[]
        {
            new TestUpdatableEntity { Name = "Entity1" },
            new TestUpdatableEntity { Name = "Entity2" },
            new TestUpdatableEntity { Name = "Entity3" }
        };

        // Act - 先重排序
        ReorderHelper.Default.ReorderEntities(entities);
        short[] originalOrders = entities.Select(e => e.Order).ToArray();
        
        // 然后在一个小范围内重新分布
        ReorderHelper.Default.RedistributeInRange(entities, 0, 100);

        // Assert
        Assert.All(entities, entity => Assert.True(entity.Order >= 0 && entity.Order <= 100));
        Assert.True(entities[0].Order < entities[1].Order);
        Assert.True(entities[1].Order < entities[2].Order);
        
        // 验证顺序与原来一致
        for (int i = 0; i < entities.Length - 1; i++)
        {
            Assert.True(entities[i].Order < entities[i + 1].Order);
        }
    }

    [Fact]
    public void Integration_MultipleCustomHelpers_ShouldWorkIndependently()
    {
        // Arrange
        ReorderOptions options1 = new ReorderOptions
        {
            ReorderSteps = [1000, 100, 10],
            ReorderStart = -30000,
            MoveStep = 1000
        };
        ReorderOptions options2 = new ReorderOptions
        {
            ReorderSteps = [500, 50, 5],
            ReorderStart = -15000,
            MoveStep = 500
        };

        ReorderHelper helper1 = new ReorderHelper(options1);
        ReorderHelper helper2 = new ReorderHelper(options2);

        TestOrderableEntity[] entities1 = new[] { new TestOrderableEntity { Name = "Entity1" } };
        TestOrderableEntity[] entities2 = new[] { new TestOrderableEntity { Name = "Entity2" } };

        // Act
        helper1.ReorderEntities(entities1);
        helper2.ReorderEntities(entities2);

        // Assert
        Assert.Equal(-30000, entities1[0].Order); // -30000 + 0*1000
        Assert.Equal(-15000, entities2[0].Order); // -15000 + 0*500
    }

    #endregion

    #region Additional Coverage Tests

    [Fact]
    public void ReorderEntities_WhenStepCausesOverflowButAdjustedStartWorks_ShouldAdjustStart()
    {
        // Arrange - 配置步长会从起始位置溢出，但可以通过调整起始位置解决
        ReorderOptions options = new ReorderOptions
        {
            ReorderSteps = [10000], // 只有一个较大的步长
            ReorderStart = 20000,   // 高起始位置
            MoveStep = 1000
        };
        ReorderHelper helper = new ReorderHelper(options);
        TestOrderableEntity[] entities = new[]
        {
            new TestOrderableEntity { Name = "Entity1" },
            new TestOrderableEntity { Name = "Entity2" }
        };

        // Act
        helper.ReorderEntities(entities);

        // Assert - 应该调整起始位置以适应步长
        Assert.True(entities[0].Order <= short.MaxValue);
        Assert.True(entities[1].Order <= short.MaxValue);
        Assert.True(entities[0].Order < entities[1].Order);
        Assert.Equal(10000, entities[1].Order - entities[0].Order); // 步长应该是10000
    }

    [Fact]
    public void ReorderEntities_WithManyEntitiesRequiringStepDowngrade_ShouldUseCorrectStep()
    {
        // Arrange - 创建足够多的实体，使得需要降级到更小的步长
        TestOrderableEntity[] entities = new TestOrderableEntity[70]; // 70个实体，1000步长会超出范围
        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = new TestOrderableEntity { Name = $"Entity{i}" };
        }

        // Act
        ReorderHelper.Default.ReorderEntities(entities);

        // Assert - 应该使用100的步长
        Assert.Equal(-30000, entities[0].Order);
        Assert.Equal(-29900, entities[1].Order); // -30000 + 100
        Assert.True(entities[entities.Length - 1].Order <= short.MaxValue);
    }

    [Fact]
    public void TryApplyMove_WithNullSourceEntity_ShouldThrowNullReferenceException()
    {
        // Arrange
        TestOrderableEntity? source = null;
        TestOrderableEntity previous = new TestOrderableEntity { Order = 1000 };

        // Act & Assert
        Assert.Throws<NullReferenceException>(() => 
            ReorderHelper.Default.TryApplyMove(source!, previous, null));
    }

    [Fact]
    public void TryApplyMove_WithZeroMoveStep_ShouldWork()
    {
        // Arrange
        ReorderOptions options = new ReorderOptions
        {
            ReorderSteps = [1000, 100, 10],
            ReorderStart = -30000,
            MoveStep = 0 // 零步长
        };
        ReorderHelper helper = new ReorderHelper(options);
        TestOrderableEntity previous = new TestOrderableEntity { Order = 1000 };
        TestOrderableEntity source = new TestOrderableEntity { Order = 500 };

        // Act
        bool result = helper.TryApplyMove(source, previous, null);

        // Assert
        Assert.True(result);
        Assert.Equal(1000, source.Order); // 1000 + 0
    }

    [Fact]
    public void TryApplyMove_WithNegativeMoveStep_ShouldWork()
    {
        // Arrange
        ReorderOptions options = new ReorderOptions
        {
            ReorderSteps = [1000, 100, 10],
            ReorderStart = -30000,
            MoveStep = -500 // 负步长
        };
        ReorderHelper helper = new ReorderHelper(options);
        TestOrderableEntity previous = new TestOrderableEntity { Order = 1000 };
        TestOrderableEntity source = new TestOrderableEntity { Order = 0 };

        // Act
        bool result = helper.TryApplyMove(source, previous, null);

        // Assert
        Assert.True(result);
        Assert.Equal(500, source.Order); // 1000 + (-500)
    }

    [Fact]
    public void RedistributeInRange_WithReverseRange_ShouldHandleGracefully()
    {
        // Arrange - maxOrder < minOrder
        TestUpdatableEntity[] entities = new[]
        {
            new TestUpdatableEntity { Name = "Entity1" },
            new TestUpdatableEntity { Name = "Entity2" }
        };

        // Act
        ReorderHelper.Default.RedistributeInRange(entities, 1000, 500); // 反向范围

        // Assert - 应该处理负的可用空间
        Assert.True(entities[0].Order >= 500);
        Assert.True(entities[0].Order <= 1000);
    }

    [Fact]
    public void RedistributeInRange_WithLargeNumberOfEntities_ShouldDistribute()
    {
        // Arrange
        TestUpdatableEntity[] entities = new TestUpdatableEntity[1000];
        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = new TestUpdatableEntity { Name = $"Entity{i}" };
        }

        // Act
        ReorderHelper.Default.RedistributeInRange(entities, short.MinValue, short.MaxValue);

        // Assert
        Assert.True(entities[0].Order >= short.MinValue);
        Assert.True(entities[999].Order <= short.MaxValue);
        
        // 验证顺序
        for (int i = 0; i < entities.Length - 1; i++)
        {
            Assert.True(entities[i].Order <= entities[i + 1].Order);
        }
    }

    [Fact]
    public void ReorderHelper_Properties_ShouldReturnCorrectValues()
    {
        // Arrange
        ReorderOptions options = new ReorderOptions
        {
            ReorderSteps = [2000, 200, 20],
            ReorderStart = -25000,
            MoveStep = 750
        };
        ReorderHelper helper = new ReorderHelper(options);

        // Act & Assert
        Assert.Equal(750, helper.MoveStep);
        Assert.Equal(-25000, helper.ReorderStart);
    }

    [Fact]
    public void ReorderOptions_ShouldSupportCollectionInitializer()
    {
        // Arrange & Act
        ReorderOptions options = new ReorderOptions
        {
            ReorderSteps = [1000, 100, 10, 1],
            ReorderStart = -30000,
            MoveStep = 1000
        };

        // Assert
        Assert.Equal(4, options.ReorderSteps.Length);
        Assert.Equal(1000, options.ReorderSteps[0]);
        Assert.Equal(1, options.ReorderSteps[3]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public void ReorderEntities_WithDifferentSizes_ShouldMaintainOrder(int entityCount)
    {
        // Arrange
        TestOrderableEntity[] entities = new TestOrderableEntity[entityCount];
        for (int i = 0; i < entityCount; i++)
        {
            entities[i] = new TestOrderableEntity { Name = $"Entity{i}" };
        }

        // Act
        ReorderHelper.Default.ReorderEntities(entities);

        // Assert
        for (int i = 0; i < entities.Length - 1; i++)
        {
            Assert.True(entities[i].Order < entities[i + 1].Order, 
                $"Entity {i} (Order: {entities[i].Order}) should be less than Entity {i + 1} (Order: {entities[i + 1].Order})");
        }
    }

    [Fact]
    public void Integration_ComplexScenario_ReorderMoveThenRedistribute()
    {
        // Arrange
        TestUpdatableEntity[] entities = new TestUpdatableEntity[5];
        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = new TestUpdatableEntity { Name = $"Entity{i}" };
        }

        // Act 1: 初始重排序
        ReorderHelper.Default.ReorderEntities(entities);
        short[] initialOrders = entities.Select(e => e.Order).ToArray();

        // Act 2: 移动第一个实体到末尾
        TestUpdatableEntity moved = entities[0];
        bool moveResult = ReorderHelper.Default.TryApplyMove(moved, entities[4], null);

        // Act 3: 在小范围内重新分布
        ReorderHelper.Default.RedistributeInRange(entities, 0, 100);

        // Assert
        Assert.True(moveResult);
        Assert.All(entities, e => Assert.True(e.Order >= 0 && e.Order <= 100));
        Assert.True(entities[0].Order < entities[1].Order);
        Assert.True(entities[1].Order < entities[2].Order);
        Assert.True(entities[2].Order < entities[3].Order);
        Assert.True(entities[3].Order < entities[4].Order);
    }

    #endregion
}
