﻿using System;
using NUnit.Framework;
using AGS.Engine;
using System.Threading.Tasks;
using AGS.API;
using Moq;

namespace Tests
{
	[TestFixture]
	public class TweensTests
	{
        private AGSEvent<IRepeatedlyExecuteEventArgs> _onRepeatedlyExecute;
		private bool _testCompleted;

		[TestFixtureSetUp]
		public void Init()
		{
            _onRepeatedlyExecute = new AGSEvent<IRepeatedlyExecuteEventArgs>();
			startTicks();
			Mock<IGameEvents> gameEvents = new Mock<IGameEvents>();

			gameEvents.Setup(g => g.OnRepeatedlyExecute).Returns(_onRepeatedlyExecute);
			Tween.OverrideGameEvents = gameEvents.Object;
		}

		[TestFixtureTearDown]
		public void TearDown()
		{
			_testCompleted = true;
		}

		[Test]
		public async Task TestTweenHue()
		{
            var resolver = ObjectTests.GetResolver();
            resolver.Build();
            AGSSprite sprite = new AGSSprite (resolver, null);
			sprite.Tint = Color.FromHsla(0, 1f, 0.75f, 100); //Lightness must be different than 1 for hue to change

			await sprite.TweenTintHue(359, 1f).Task;
			Assert.AreEqual(359, sprite.Tint.GetHue());

			await sprite.TweenTintHue(0, 1f, Ease.QuadIn).Task;
			Assert.AreEqual(0, sprite.Tint.GetHue());
		}

		[Test]
		public async Task TestTweenSaturation()
		{
			var resolver = ObjectTests.GetResolver();
            resolver.Build();
            AGSSprite sprite = new AGSSprite (resolver, null);
			sprite.Tint = Color.FromHsla(100, 1f, 0.5f, 100); //Lightness must be different than 1 for saturation to change

			await sprite.TweenTintSaturation(0f, 1f, Ease.QuadIn).Task;
			Assert.IsTrue(Math.Abs(sprite.Tint.GetSaturation()) < 0.0001f);

			await sprite.TweenTintSaturation(1f, 1f).Task;
			Assert.IsTrue(Math.Abs(1f - sprite.Tint.GetSaturation()) < 0.0001f);
		}

		private async void startTicks()
		{
			await tick();
		}

		private async Task tick()
		{
			if (_testCompleted) return;
			await Task.Delay(10);
            await _onRepeatedlyExecute.InvokeAsync(new RepeatedlyExecuteEventArgs());
			await tick();
		}
	}
}

