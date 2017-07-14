﻿using System;
using Moq;
using NUnit.Framework;

namespace Nimator
{
    [TestFixture]
    public class NimatorEngineTests
    {
        [SetUp]
        public void SetUp()
        {
            var now = new DateTime(2016, 8, 16, 13, 0, 0);
            AmbientTimeProvider.SetNewTimeProvider(() =>
            {
                now = now.AddMilliseconds(15); // There's 15 ms between each call to the time provider.
                return now;
            });
        }

        [Test]
        public void Constructor_WhenPassedNull_ThrowsException()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new NimatorEngine(null));
            Assert.That(exception.ParamName, Is.EqualTo("layers"));
        }

        [Test]
        public void CheckLayers_WhenLayerReturnsNullResult_ReturnsCriticalResult()
        {
            var layer1 = new Mock<ILayer>();
            layer1.Setup(l => l.Run()).Returns<LayerResult>(null);
            var nimator = new NimatorEngine(new[] {layer1.Object});
            var result = nimator.RunSafe();

            Assert.That(result.Level, Is.EqualTo(NotificationLevel.Critical));
            Assert.That(result.Message.ToLowerInvariant(), Does.Contain("nimator"));
            Assert.That(result.Message.ToLowerInvariant(), Does.Contain("failed"));

            var fullText = result.RenderPlainText(NotificationLevel.Error).ToLowerInvariant();
            Assert.That(fullText, Does.Contain("layer"));
            Assert.That(fullText, Does.Contain("returned no result"));
        }

        [Test]
        public void CheckLayers_WhenCalled_CallsCheckInEachLayer()
        {
            var layer1 = new Mock<ILayer>().WithResult(NotificationLevel.Okay);
            var layer2 = new Mock<ILayer>().WithResult(NotificationLevel.Okay);
            var nimator = new NimatorEngine(new[] {layer1.Object, layer2.Object});
            var result = nimator.RunSafe();
            layer1.Verify(l => l.Run(), Times.Once);
            layer2.Verify(l => l.Run(), Times.Once);
        }

        [Test]
        public void CheckLayers_WhenCalled_ReturnsListOfLayerResults()
        {
            var layerResult1 = new LayerResult("layer 1", new CheckResult[0]);
            var layer1 = new Mock<ILayer>();
            layer1.Setup(l => l.Run()).Returns(layerResult1);

            var layerResult2 = new LayerResult("layer 2", new CheckResult[0]);
            var layer2 = new Mock<ILayer>();
            layer2.Setup(l => l.Run()).Returns(layerResult2);

            var nimator = new NimatorEngine(new[] {layer1.Object, layer2.Object});
            var result = nimator.RunSafe();

            Assert.That(result.LayerResults[0], Is.EqualTo(layerResult1));
            Assert.That(result.LayerResults[1], Is.EqualTo(layerResult2));
        }

        [Test]
        public void CheckLayers_WhenCalled_SetsStartAndEndFromAmbientTimeProvider()
        {
            var now = new DateTime(2016, 8, 16, 13, 0, 0);
            AmbientTimeProvider.SetNewTimeProvider(() =>
            {
                now = now.AddSeconds(15);
                return now;
            });

            var layer1 = new Mock<ILayer>().WithResult(NotificationLevel.Okay);
            var layer2 = new Mock<ILayer>().WithResult(NotificationLevel.Okay);
            var nimator = new NimatorEngine(new[] {layer1.Object, layer2.Object});

            var result = nimator.RunSafe();

            Assert.That(result.Started, Is.EqualTo(new DateTime(2016, 8, 16, 13, 0, 15)));
            Assert.That(result.Finished, Is.GreaterThan(result.Started));
        }

        [Test]
        public void CheckLayers_WhenAmbientTimeProvderCrashes_WillReturnCriticalResult()
        {
            AmbientTimeProvider.SetNewTimeProvider(() => { throw new Exception("Something truly terrible happened..."); });
            var nimator = new NimatorEngine(new ILayer[0]);

            var result = nimator.RunSafe();

            Assert.That(result.Level, Is.EqualTo(NotificationLevel.Critical));
            Assert.That(result.RenderPlainText(NotificationLevel.Error), Does.Contain("truly terrible"));
        }

        [TestCase(NotificationLevel.Error)]
        [TestCase(NotificationLevel.Critical)]
        public void CheckLayers_WhenOneLayerHasProblems_StopsProcessing(NotificationLevel thresholdLevel)
        {
            var layer1 = new Mock<ILayer>().WithResult(thresholdLevel);
            var layer2 = new Mock<ILayer>();
            var nimator = new NimatorEngine(new[] {layer1.Object, layer2.Object});

            var result = nimator.RunSafe();

            layer2.Verify(l => l.Run(), Times.Never);
        }

        [Test]
        public void CheckLayers_WhenOneLayerThrowsException_SetsTimes()
        {
            var layer1 = new Mock<ILayer>();
            layer1.Setup(l => l.Run()).Throws<Exception>();

            var nimator = new NimatorEngine();

            nimator.AddLayer(layer1.Object);

            var result = nimator.RunSafe();

            Assert.That(result.Level, Is.EqualTo(NotificationLevel.Critical));
            Assert.That(result.Started, Is.Not.EqualTo(default(DateTime)));
            Assert.That(result.Finished, Is.Not.EqualTo(default(DateTime)));
        }

        [Test]
        public void CheckLayers_WhenOneLayerThrowsAggregateExceptionWithOneInnerException_GetUsefulMessage()
        {
            var layer1 = new Mock<ILayer>();
            layer1.Setup(l => l.Run()).Throws(new AggregateException(new[] { new Exception("failure1") }));

            var nimator = new NimatorEngine();

            nimator.AddLayer(layer1.Object);

            var result = nimator.RunSafe();

            Assert.That(result.Level, Is.EqualTo(NotificationLevel.Critical));
            Assert.That(result.RenderPlainText(NotificationLevel.Critical), Does.Contain("failure1"));
        }

        [Test]
        public void CheckLayers_WhenOneLayerThrowsAggregateExceptionWithMultipleInnerException_GetUsefulMessage()
        {
            var layer1 = new Mock<ILayer>();
            layer1.Setup(l => l.Run()).Throws(new AggregateException(new[]
                {new Exception("failure1"), new Exception("failure2"), new Exception("failure3")}));

            var nimator = new NimatorEngine();

            nimator.AddLayer(layer1.Object);

            var result = nimator.RunSafe();

            Assert.That(result.Level, Is.EqualTo(NotificationLevel.Critical));
            Assert.That(result.RenderPlainText(NotificationLevel.Critical), Does.Contain("failure1"));
            Assert.That(result.RenderPlainText(NotificationLevel.Critical), Does.Contain("failure2"));
            Assert.That(result.RenderPlainText(NotificationLevel.Critical), Does.Contain("failure3"));
        }

        [Test]
        public void CheckLayers_WhenOneLayerThrowsExceptionWithInnerException_GetUsefulMessage()
        {
            var layer1 = new Mock<ILayer>();
            layer1.Setup(l => l.Run()).Throws(new Exception("failure1", new Exception("innerFailure")));

            var nimator = new NimatorEngine();

            nimator.AddLayer(layer1.Object);

            var result = nimator.RunSafe();

            Assert.That(result.Level, Is.EqualTo(NotificationLevel.Critical));
            Assert.That(result.RenderPlainText(NotificationLevel.Critical), Does.Contain("failure1"));
            Assert.That(result.RenderPlainText(NotificationLevel.Critical), Does.Contain("innerFailure"));
        }

        [Test]
        public void AddLayer_WhenPassedIndividualParts_CreatesNewLayer()
        {
            var nimator = new NimatorEngine();

            var result1 = new CheckResult("check A", NotificationLevel.Warning);
            var check1 = new Mock<ICheck>();
            check1.Setup(c => c.RunAsync()).Returns(result1.AsTaskResult());

            nimator.AddLayer("dummy name", new []{ check1.Object });

            var result = nimator.RunSafe();

            Assert.That(result.Level, Is.EqualTo(NotificationLevel.Warning));
        }
    }
}
