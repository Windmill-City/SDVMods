﻿// Thanks to Pathoschild's Lookup Anything, which provided the pattern code for this menu!

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using Pathoschild.Stardew.UIF;
using System.Linq;
using TwilightCore.StardewValley;

namespace ClimatesOfFerngillRebuild
{
    /// <summary>A UI which shows information about an item.</summary>
    internal class WeatherMenu : IClickableMenu
    {
        /*********
        ** Properties
        *********/

        /// <summary>Encapsulates logging and monitoring.</summary>
        private readonly IMonitor Monitor;

        /// <summary>Simplifies access to private game code.</summary>
        private readonly IReflectionHelper Reflection;

        /// <summary> Configuration Options </summary>
        private WeatherConfig OurConfig;
        
        /// <summary>The aspect ratio of the page background.</summary>
        private readonly Vector2 AspectRatio = new Vector2(Sprites.Letter.Sprite.Width, Sprites.Letter.Sprite.Height);

        /// <summary>The spacing around the scroll buttons.</summary>
        private readonly int ScrollButtonGutter = 15;

        /// <summary> To da Moon, Princess!  </summary>
        private SDVMoon OurMoon;

        /// <summary> The current weather status </summary>
        private WeatherConditions CurrentWeather;

        /// <summary> Our Icons </summary>
        private Sprites.Icons IconSheet;

        ///<summary>This contains the text for various things</summary>
        private ITranslationHelper Helper;

        /// <summary>Whether the game's draw mode has been validated for compatibility.</summary>
        private bool ValidatedDrawMode;

        /*********
        ** Public methods
        *********/
        /****
        ** Constructors
        ****/
        /// <summary>Construct an instance.</summary>
        /// <param name="monitor">Encapsulates logging and monitoring.</param>
        public WeatherMenu(IMonitor monitor, IReflectionHelper reflectionHelper, Sprites.Icons Icon, ITranslationHelper Helper, WeatherConditions weat, SDVMoon Termina, 
               WeatherConfig ModCon)
        {
            // save data
            this.Monitor = monitor;
            this.Reflection = reflectionHelper;
            this.CurrentWeather = weat;
            this.IconSheet = Icon;
            this.OurMoon = Termina;
            this.OurConfig = ModCon;

            // update layout
            this.UpdateLayout();
        }

        /****
        ** Events
        ****/
        /// <summary>The method invoked when the player left-clicks on the lookup UI.</summary>
        /// <param name="x">The X-position of the cursor.</param>
        /// <param name="y">The Y-position of the cursor.</param>
        /// <param name="playSound">Whether to enable sound.</param>
        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            this.HandleLeftClick(x, y);
        }

        /// <summary>The method invoked when the player right-clicks on the lookup UI.</summary>
        /// <param name="x">The X-position of the cursor.</param>
        /// <param name="y">The Y-position of the cursor.</param>
        /// <param name="playSound">Whether to enable sound.</param>
        public override void receiveRightClick(int x, int y, bool playSound = true) { }

        /// <summary>The method called when the game window changes size.</summary>
        /// <param name="oldBounds">The former viewport.</param>
        /// <param name="newBounds">The new viewport.</param>
        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            this.UpdateLayout();
        }

        /// <summary>The method called when the player presses a controller button.</summary>
        /// <param name="button">The controller button pressed.</param>
        public override void receiveGamePadButton(Buttons button)
        {
            switch (button)
            {
                // left click
                case Buttons.A:
                    Point p = Game1.getMousePosition();
                    this.HandleLeftClick(p.X, p.Y);
                    break;

                // exit
                case Buttons.B:
                    this.exitThisMenu();
                    break;
            }
        }

        /****
        ** Methods
        ****/

        /// <summary>Handle a left-click from the player's mouse or controller.</summary>
        /// <param name="x">The x-position of the cursor.</param>
        /// <param name="y">The y-position of the cursor.</param>
        public void HandleLeftClick(int x, int y)
        {
            // close menu when clicked outside
            if (!this.isWithinBounds(x, y))
                this.exitThisMenu();
        }

        public string FirstCharToUpper(string input)
        {
            if (String.IsNullOrEmpty(input))
                throw new ArgumentException("ARGH!");
            return input.First().ToString().ToUpper() + input.Substring(1);
        }

        /// <summary>Render the UI.</summary>
        /// <param name="spriteBatch">The sprite batch being drawn.</param>
        public override void draw(SpriteBatch spriteBatch)
        {
            // disable when game is using immediate sprite sorting
            if (!this.ValidatedDrawMode)
            {
                IPrivateField<SpriteSortMode> sortModeField =
                    this.Reflection.GetPrivateField<SpriteSortMode>(Game1.spriteBatch, "spriteSortMode", required: false) // XNA
                    ?? this.Reflection.GetPrivateField<SpriteSortMode>(Game1.spriteBatch, "_sortMode"); // MonoGame
                if (sortModeField.GetValue() == SpriteSortMode.Immediate)
                {
                    this.Monitor.Log("Aborted the weather draw because the game's current rendering mode isn't compatible with the mod's UI. This only happens in rare cases (e.g. the Stardew Valley Fair).", LogLevel.Warn);
                    this.exitThisMenu(playSound: false);
                    return;
                }
                this.ValidatedDrawMode = true;
            }

            // calculate dimensions
            int x = this.xPositionOnScreen;
            int y = this.yPositionOnScreen;
            const int gutter = 15;
            float leftOffset = gutter;
            float topOffset = gutter;
            float contentWidth = this.width - gutter * 2;
            float contentHeight = this.height - gutter * 2;
            //int tableBorderWidth = 1;

            // get font
            SpriteFont font = Game1.smallFont;
            float lineHeight = font.MeasureString("ABC").Y;

            //at this point I'm going to manually put this in as I don't need in many places,
            // and I kinda want to have this where I can understand what it's for 
            float spaceWidth = DrawHelper.GetSpaceWidth(font);

            // draw background
            // (This uses a separate sprite batch because it needs to be drawn before the
            // foreground batch, and we can't use the foreground batch because the background is
            // outside the clipping area.)
            using (SpriteBatch backgroundBatch = new SpriteBatch(Game1.graphics.GraphicsDevice))
            {
                backgroundBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, null);
                backgroundBatch.DrawSprite(Sprites.Letter.Sheet, Sprites.Letter.Sprite, x, y, scale: width / (float)Sprites.Letter.Sprite.Width);
                backgroundBatch.End();
            }

            // draw foreground
            // (This uses a separate sprite batch to set a clipping area for scrolling.)
            using (SpriteBatch contentBatch = new SpriteBatch(Game1.graphics.GraphicsDevice))
            {
                // begin draw
                GraphicsDevice device = Game1.graphics.GraphicsDevice;
                device.ScissorRectangle = new Rectangle(x + gutter, y + gutter, (int)contentWidth, (int)contentHeight);
                contentBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, new RasterizerState { ScissorTestEnable = true });

                // draw portrait
                spriteBatch.Draw(IconSheet.source, new Vector2(x + leftOffset, y + topOffset), 
                    IconSheet.GetWeatherSprite(CurrentWeather.TodayWeather), Color.White);
                leftOffset += 72;

                // draw fields
                float wrapWidth = this.width - leftOffset - gutter;
                {
                    // draw high and low
                    {
                        Vector2 descSize = contentBatch.DrawTextBlock(font, Helper.Get("weather-menu.opening", new { season = Game1.currentSeason, day = Game1.dayOfMonth }), 
                            new Vector2(x + leftOffset, y + topOffset), wrapWidth);
                        topOffset += descSize.Y;
                                                
                        topOffset += descSize.Y;
                        topOffset += lineHeight;
                        //build the temperature display
                        string Temperature = "";

                        if (OurConfig.ShowBothScales)
                            Temperature = Helper.Get("weather-menu.temp_bothscales", new
                            { highTempC = CurrentWeather.GetTodayHigh(),
                              lowTempC = CurrentWeather.GetTodayLow(),
                              highTempF = CurrentWeather.GetTodayHighF(),
                              lowTempF = CurrentWeather.GetTodayLowF(),
                            });
                        else
                            Temperature = Helper.Get("weather-menu.temp_onlyscales", new
                            {
                                highTempC = CurrentWeather.GetTodayHigh(),
                                lowTempC = CurrentWeather.GetTodayLow()
                            });

                        //Output today's weather
                        string weatherString = "";
                        if (CurrentWeather.TodayWeather != Game1.weather_festival)
                        {
                            weatherString = Helper.Get("weather-menu.fore_today_notFest", new { currentConditions = "blah", Temperature = Temperature });
                                
                                //$"{OurText.WRToday} {WeatherHelper.DescWeather(CurrentWeather.CurrentConditions(), Game1.currentSeason)} with {Temperature}";
                        }
                        else
                        {
                            weatherString = Helper.Get("weather-menu.fore_today_festival"), new { festival = SDVUtilities.GetFestivalName(), Temperature = Temperature });
                            //$"{OurText.WRFestivalToday} {SDVUtilities.GetFestivalName()} {OurText.WRWith} {Temperature}";
                        }

                        Vector2 nameSize = contentBatch.DrawTextBlock(font, weatherString, new Vector2(x + leftOffset, y + topOffset), wrapWidth);
                        topOffset += nameSize.Y;
                        topOffset += lineHeight;


                        //Output Tomorrow's weather
                        if (!(Utility.isFestivalDay(Game1.dayOfMonth +1, Game1.currentSeason)))
                        {
                            weatherString = $"{OurText.WRTomorrow} { WeatherHelper.DescWeather((SDVWeather)Game1.weatherForTomorrow, Game1.currentSeason)}.";
                        }
                        else
                        {
                            weatherString = $"{OurText.WRFestivalTomorrow} {SDVUtilities.GetTomorrowFestivalName()}";
                        }

                        Vector2 tomSize = contentBatch.DrawTextBlock(font, weatherString, new Vector2(x + leftOffset, y + topOffset), wrapWidth);

                        topOffset += tomSize.Y;
                    }

                    // draw spacer
                    topOffset += lineHeight;
                    
                    if (CurrentWeather.IsDangerousWeather())
                    {
                        Vector2 statusSize = contentBatch.DrawTextBlock(font, $"{OurText.WRAlert} {CurrentWeather.GetHazardMessage()}", new Vector2(x + leftOffset, y + topOffset), wrapWidth, bold: true);
                        topOffset += statusSize.Y;
                        topOffset += lineHeight;
                    }

                }

                //draw moon info
                spriteBatch.Draw(IconSheet.source, new Vector2(x + 15, y + topOffset), IconSheet.GetMoonSprite(OurMoon.CurrPhase), Color.White);
                
                Vector2 moonText = contentBatch.DrawTextBlock(font, $"{OurText.WRMoonPhase} {SDVMoon.DescribeMoonPhase(OurMoon.CurrPhase)}.", new Vector2(x + leftOffset, y + topOffset), wrapWidth);


                // end draw
                contentBatch.End();
            }
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Update the layout dimensions based on the current game scale.</summary>
        private void UpdateLayout()
        {
            // update size
            this.width = Math.Min(Game1.tileSize * 14, Game1.viewport.Width);
            this.height = Math.Min((int)(this.AspectRatio.Y / this.AspectRatio.X * this.width), Game1.viewport.Height);

            // update position
            Vector2 origin = Utility.getTopLeftPositionForCenteringOnScreen(this.width, this.height);
            this.xPositionOnScreen = (int)origin.X;
            this.yPositionOnScreen = (int)origin.Y;

            // update up/down buttons
            int x = this.xPositionOnScreen;
            int y = this.yPositionOnScreen;
            int gutter = this.ScrollButtonGutter;
            float contentHeight = this.height - gutter * 2;
        }

        /// <summary>The method invoked when an unhandled exception is intercepted.</summary>
        /// <param name="ex">The intercepted exception.</param>
        private void OnDrawError(Exception ex)
        {
            Monitor.Log($"handling an error in the draw code {ex}", LogLevel.Error);
        }
    }
}