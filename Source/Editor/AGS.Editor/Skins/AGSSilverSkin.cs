﻿using AGS.API;
using AGS.Engine;

namespace AGS.Editor
{
    public class AGSSilverSkin
    {
        private readonly AGSColoredSkin _skin;

        public AGSSilverSkin(IGLUtils glUtils, IGameSettings settings)
        {
            var buttonBorder = AGSBorders.SolidColor(glUtils, settings, GameViewColors.Border, 1f);
            var pushedButtonBorder = AGSBorders.SolidColor(glUtils, settings, GameViewColors.Border, 2f);
            _skin = new AGSColoredSkin
            {
                ButtonIdleAnimation = new ButtonAnimation(buttonBorder, null, GameViewColors.Button),
                ButtonHoverAnimation = new ButtonAnimation(buttonBorder, null, GameViewColors.HoveredButton),
                ButtonPushedAnimation = new ButtonAnimation(pushedButtonBorder, null, GameViewColors.PushedButton),
                TextBoxBackColor = GameViewColors.Textbox,
                TextBoxBorderStyle = AGSBorders.SolidColor(glUtils, settings, Colors.WhiteSmoke, 1f),
                CheckboxCheckedAnimation = new ButtonAnimation(buttonBorder, null, GameViewColors.Button),
                CheckboxNotCheckedAnimation = new ButtonAnimation(buttonBorder, null, GameViewColors.Button),
                CheckboxHoverCheckedAnimation = new ButtonAnimation(buttonBorder, null, GameViewColors.HoveredButton),
                CheckboxHoverNotCheckedAnimation = new ButtonAnimation(buttonBorder, null, GameViewColors.HoveredButton),
                DialogBoxColor = GameViewColors.Panel,
                DialogBoxBorder = AGSBorders.SolidColor(glUtils, settings, Colors.WhiteSmoke, 2f)
            };
        }

        public ISkin CreateSkin()
        {
            return _skin.CreateSkin();
        }
    }
}
