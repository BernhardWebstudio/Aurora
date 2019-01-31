﻿using System;
using System.Collections.Generic;
using System.Linq;
using Aurora.EffectsEngine;
using System.Drawing;
using Aurora.Profiles;
using System.Windows.Controls;
using Newtonsoft.Json;
using Aurora.Devices;
using Aurora.Devices.Layout;
using Aurora.Devices.Layout.Layouts;

namespace Aurora.Settings.Layers
{
    public class WrapperLightsLayerHandlerProperties : LayerHandlerProperties<WrapperLightsLayerHandlerProperties>
    {
        // Color Enhancing
        public bool? _ColorEnhanceEnabled { get; set; }
        [JsonIgnore]
        public bool ColorEnhanceEnabled { get { return Logic._ColorEnhanceEnabled ?? _ColorEnhanceEnabled ?? false; } }

        public int? _ColorEnhanceMode { get; set; }
        [JsonIgnore]
        public int ColorEnhanceMode { get { return Logic._ColorEnhanceMode ?? _ColorEnhanceMode ?? 0; } }

        public int? _ColorEnhanceColorFactor { get; set; }
        [JsonIgnore]
        public int ColorEnhanceColorFactor { get { return Logic._ColorEnhanceColorFactor ?? _ColorEnhanceColorFactor ?? 0; } }

        public float? _ColorEnhanceColorHSVSine { get; set; }
        [JsonIgnore]
        public float ColorEnhanceColorHSVSine { get { return Logic._ColorEnhanceColorHSVSine ?? _ColorEnhanceColorHSVSine ?? 0.0f; } }

        public float? _ColorEnhanceColorHSVGamma { get; set; }
        [JsonIgnore]
        public float ColorEnhanceColorHSVGamma { get { return Logic._ColorEnhanceColorHSVGamma ?? _ColorEnhanceColorHSVGamma ?? 0.0f; } }

        // Key cloning
        [JsonIgnore]
        public Dictionary<DeviceLED, KeySequence> CloningMap => Logic._CloningMap ?? _CloningMap ?? new Dictionary<DeviceLED, KeySequence>();
        public Dictionary<DeviceLED, KeySequence> _CloningMap { get; set; }

        public WrapperLightsLayerHandlerProperties() : base() { }

        public WrapperLightsLayerHandlerProperties(bool arg = false) : base(arg) { }

        public override void Default()
        {
            base.Default();
            _PrimaryColor = Utils.ColorUtils.GenerateRandomColor();

            _ColorEnhanceEnabled = true;
            _ColorEnhanceMode = 0;
            _ColorEnhanceColorFactor = 90;
            _ColorEnhanceColorHSVSine = 0.1f;
            _ColorEnhanceColorHSVGamma = 2.5f;
            _CloningMap = new Dictionary<DeviceLED, KeySequence>();
        }
    }

    public class WrapperLightsLayerHandler : LayerHandler<WrapperLightsLayerHandlerProperties>
    {
        internal int[] bitmap = new int[126];
        internal Dictionary<DeviceLED, Color> extra_keys = new Dictionary<DeviceLED, Color>();
        internal Color last_fill_color = Color.Black;
        internal Dictionary<DeviceLED, KeyEffect> key_effects = new Dictionary<DeviceLED, KeyEffect>();
        internal EntireEffect current_effect = null;

        internal Dictionary<DeviceLED, Color> colors = new Dictionary<DeviceLED, Color>();

        public WrapperLightsLayerHandler()
        {
            this._ID = "WrapperLights";
        }

        protected override UserControl CreateControl()
        {
            return new Control_WrapperLightsLayer(this);
        }

        public override EffectLayer Render(IGameState gamestate)
        {
            if (!(gamestate is GameState_Wrapper))
                return new EffectLayer();

            Queue<EffectLayer> layers = new Queue<EffectLayer>();

            EffectLayer colorfill_layer = new EffectLayer("Aurora Wrapper - Color Fill", GetBoostedColor(last_fill_color));

            layers.Enqueue(colorfill_layer);

            EffectLayer bitmap_layer = new EffectLayer("Aurora Wrapper - Bitmap");

            foreach (var key in GlobalDeviceLayout.Instance.AllLeds)
            {

                // This checks if a key is already being cloned over and thus should be prevented from being re-set by the
                // normal wrapper. Fixes issues with some clones not working. Thanks to @Gurjot95 for finding it :)
                if (Properties.CloningMap.Values.Any(sequence => sequence.keys.Contains(key)))
                    continue;


                if (extra_keys.ContainsKey(key))
                {
                    bitmap_layer.Set(key, GetBoostedColor(extra_keys[key]));

                    // Do the key cloning
                    if (Properties.CloningMap.ContainsKey(key))
                        bitmap_layer.Set(Properties.CloningMap[key], GetBoostedColor(extra_keys[key]));
                }
                else
                {
                    Devices.Logitech.Logitech_keyboardBitmapKeys logi_key = Devices.Logitech.LogitechDevice.ToLogitechBitmap(key);

                    if (logi_key != Devices.Logitech.Logitech_keyboardBitmapKeys.UNKNOWN && bitmap.Length > 0) {
                        var color = GetBoostedColor(Utils.ColorUtils.GetColorFromInt(bitmap[(int)logi_key / 4]));
                        bitmap_layer.Set(key, color);

                        // Key cloning
                        if (Properties.CloningMap.ContainsKey(key))
                            bitmap_layer.Set(Properties.CloningMap[key], color);
                    }
                }
            }

            layers.Enqueue(bitmap_layer);

            EffectLayer effects_layer = new EffectLayer("Aurora Wrapper - Effects");

            DeviceLED[] effect_keys = key_effects.Keys.ToArray();
            long currentTime = Utils.Time.GetMillisecondsSinceEpoch();

            foreach (var key in effect_keys)
            {
                if (key_effects[key].duration != 0 && key_effects[key].timeStarted + key_effects[key].duration <= currentTime)
                {
                    key_effects.Remove(key);
                }
                else
                {
                    if (key_effects[key] is LogiFlashSingleKey)
                        effects_layer.Set((key_effects[key] as LogiFlashSingleKey).key, GetBoostedColor((key_effects[key] as LogiFlashSingleKey).GetColor(currentTime - (key_effects[key] as LogiFlashSingleKey).timeStarted)));
                    else if (key_effects[key] is LogiPulseSingleKey)
                        effects_layer.Set((key_effects[key] as LogiPulseSingleKey).key, GetBoostedColor((key_effects[key] as LogiPulseSingleKey).GetColor(currentTime - (key_effects[key] as LogiPulseSingleKey).timeStarted)));
                    else
                        effects_layer.Set((key_effects[key] as KeyEffect).key, GetBoostedColor((key_effects[key] as KeyEffect).GetColor(currentTime - (key_effects[key] as KeyEffect).timeStarted)));

                }
            }

            layers.Enqueue(effects_layer);

            EffectLayer entire_effect_layer = new EffectLayer("Aurora Wrapper - EntireKB effect");

            if (current_effect != null)
            {
                if (current_effect is LogiFlashLighting)
                    (current_effect as LogiFlashLighting).SetEffect(entire_effect_layer, currentTime - (current_effect as LogiFlashLighting).timeStarted);
                else if (current_effect is LogiPulseLighting)
                    (current_effect as LogiPulseLighting).SetEffect(entire_effect_layer, currentTime - (current_effect as LogiPulseLighting).timeStarted);
                else
                    current_effect.SetEffect(entire_effect_layer, currentTime - current_effect.timeStarted);
            }

            layers.Enqueue(entire_effect_layer);

            EffectLayer[] layersArray = layers.ToArray();
            EffectLayer finalLayer = layersArray[0];
            for (int i = 1; i < layersArray.Length; i++)
                finalLayer += layersArray[i];

            return finalLayer;
        }

        public override void SetGameState(IGameState gamestate)
        {
            if (!(gamestate is GameState_Wrapper))
                return;

            GameState_Wrapper ngw_state = (gamestate as GameState_Wrapper);

            if (ngw_state.Sent_Bitmap.Length != 0)
                bitmap = ngw_state.Sent_Bitmap;

            SetExtraKey(KeyboardKeys.LOGO, ngw_state.Extra_Keys.logo);
            SetExtraKey(KeyboardKeys.LOGO2, ngw_state.Extra_Keys.badge);
            //TODO: Deal with MouseLights.ALL
            SetExtraKey(MouseLights.All, ngw_state.Extra_Keys.peripheral);
            //Reversing the mousepad lights from left to right, razer takes it from right to left
            //TODO: Deal with these
            /*SetExtraKey(Devices.DeviceKeys.Peripheral, ngw_state.Extra_Keys.peripheral);
            SetExtraKey(Devices.DeviceKeys.MOUSEPADLIGHT15, ngw_state.Extra_Keys.mousepad1);
            SetExtraKey(Devices.DeviceKeys.MOUSEPADLIGHT14, ngw_state.Extra_Keys.mousepad2);
            SetExtraKey(Devices.DeviceKeys.MOUSEPADLIGHT13, ngw_state.Extra_Keys.mousepad3);
            SetExtraKey(Devices.DeviceKeys.MOUSEPADLIGHT12, ngw_state.Extra_Keys.mousepad4);
            SetExtraKey(Devices.DeviceKeys.MOUSEPADLIGHT11, ngw_state.Extra_Keys.mousepad5);
            SetExtraKey(Devices.DeviceKeys.MOUSEPADLIGHT10, ngw_state.Extra_Keys.mousepad6);
            SetExtraKey(Devices.DeviceKeys.MOUSEPADLIGHT9, ngw_state.Extra_Keys.mousepad7);
            SetExtraKey(Devices.DeviceKeys.MOUSEPADLIGHT8, ngw_state.Extra_Keys.mousepad8);
            SetExtraKey(Devices.DeviceKeys.MOUSEPADLIGHT7, ngw_state.Extra_Keys.mousepad9);
            SetExtraKey(Devices.DeviceKeys.MOUSEPADLIGHT6, ngw_state.Extra_Keys.mousepad10);
            SetExtraKey(Devices.DeviceKeys.MOUSEPADLIGHT5, ngw_state.Extra_Keys.mousepad11);
            SetExtraKey(Devices.DeviceKeys.MOUSEPADLIGHT4, ngw_state.Extra_Keys.mousepad12);
            SetExtraKey(Devices.DeviceKeys.MOUSEPADLIGHT3, ngw_state.Extra_Keys.mousepad13);
            SetExtraKey(Devices.DeviceKeys.MOUSEPADLIGHT2, ngw_state.Extra_Keys.mousepad14);
            SetExtraKey(Devices.DeviceKeys.MOUSEPADLIGHT1, ngw_state.Extra_Keys.mousepad15);*/
            SetExtraKey(KeyboardKeys.G1, ngw_state.Extra_Keys.G1);
            SetExtraKey(KeyboardKeys.G2, ngw_state.Extra_Keys.G2);
            SetExtraKey(KeyboardKeys.G3, ngw_state.Extra_Keys.G3);
            SetExtraKey(KeyboardKeys.G4, ngw_state.Extra_Keys.G4);
            SetExtraKey(KeyboardKeys.G5, ngw_state.Extra_Keys.G5);
            SetExtraKey(KeyboardKeys.G6, ngw_state.Extra_Keys.G6);
            SetExtraKey(KeyboardKeys.G7, ngw_state.Extra_Keys.G7);
            SetExtraKey(KeyboardKeys.G8, ngw_state.Extra_Keys.G8);
            SetExtraKey(KeyboardKeys.G9, ngw_state.Extra_Keys.G9);
            SetExtraKey(KeyboardKeys.G10, ngw_state.Extra_Keys.G10);
            SetExtraKey(KeyboardKeys.G11, ngw_state.Extra_Keys.G11);
            SetExtraKey(KeyboardKeys.G12, ngw_state.Extra_Keys.G12);
            SetExtraKey(KeyboardKeys.G13, ngw_state.Extra_Keys.G13);
            SetExtraKey(KeyboardKeys.G14, ngw_state.Extra_Keys.G14);
            SetExtraKey(KeyboardKeys.G15, ngw_state.Extra_Keys.G15);
            SetExtraKey(KeyboardKeys.G16, ngw_state.Extra_Keys.G16);
            SetExtraKey(KeyboardKeys.G17, ngw_state.Extra_Keys.G17);
            SetExtraKey(KeyboardKeys.G18, ngw_state.Extra_Keys.G18);
            SetExtraKey(KeyboardKeys.G19, ngw_state.Extra_Keys.G19);
            SetExtraKey(KeyboardKeys.G20, ngw_state.Extra_Keys.G20);

            if (ngw_state.Command.Equals("SetLighting"))
            {
                Color newfill = Color.FromArgb(ngw_state.Command_Data.red_start, ngw_state.Command_Data.green_start, ngw_state.Command_Data.blue_start);

                if (!last_fill_color.Equals(newfill))
                {
                    last_fill_color = newfill;

                    for (int i = 0; i < bitmap.Length; i++)
                    {
                        bitmap[i] = (int)(((int)ngw_state.Command_Data.red_start << 16) | ((int)ngw_state.Command_Data.green_start << 8) | ((int)ngw_state.Command_Data.blue_start));
                    }
                }
            }
            else if (ngw_state.Command.Equals("SetLightingForKeyWithKeyName") || ngw_state.Command.Equals("SetLightingForKeyWithScanCode"))
            {
                var bitmap_key = Devices.Logitech.LogitechDevice.ToLogitechBitmap((LedCSharp.keyboardNames)(ngw_state.Command_Data.key));

                if (bitmap_key != Devices.Logitech.Logitech_keyboardBitmapKeys.UNKNOWN)
                {
                    bitmap[(int)bitmap_key / 4] = (int)(((int)ngw_state.Command_Data.red_start << 16) | ((int)ngw_state.Command_Data.green_start << 8) | ((int)ngw_state.Command_Data.blue_start));
                }
            }
            else if (ngw_state.Command.Equals("FlashSingleKey"))
            {
                DeviceLED dev_key = Devices.Logitech.LogitechDevice.ToDeviceKey((LedCSharp.keyboardNames)(ngw_state.Command_Data.key)).GetDeviceLED();
                LogiFlashSingleKey neweffect = new LogiFlashSingleKey(dev_key, Color.FromArgb(ngw_state.Command_Data.red_start, ngw_state.Command_Data.green_start, ngw_state.Command_Data.blue_start),
                        ngw_state.Command_Data.duration,
                        ngw_state.Command_Data.interval
                        );

                if (key_effects.ContainsKey(dev_key))
                    key_effects[dev_key] = neweffect;
                else
                    key_effects.Add(dev_key, neweffect);

            }
            else if (ngw_state.Command.Equals("PulseSingleKey"))
            {
                DeviceLED dev_key = Devices.Logitech.LogitechDevice.ToDeviceKey((LedCSharp.keyboardNames)(ngw_state.Command_Data.key)).GetDeviceLED();
                long duration = ngw_state.Command_Data.interval == 0 ? 0 : ngw_state.Command_Data.duration;

                LogiPulseSingleKey neweffect = new LogiPulseSingleKey(dev_key, Color.FromArgb(ngw_state.Command_Data.red_start, ngw_state.Command_Data.green_start, ngw_state.Command_Data.blue_start),
                    Color.FromArgb(ngw_state.Command_Data.red_end, ngw_state.Command_Data.green_end, ngw_state.Command_Data.blue_end),
                        duration
                        );

                if (key_effects.ContainsKey(dev_key))
                    key_effects[dev_key] = neweffect;
                else
                    key_effects.Add(dev_key, neweffect);
            }
            else if (ngw_state.Command.Equals("PulseLighting"))
            {
                current_effect = new LogiPulseLighting(
                    Color.FromArgb(ngw_state.Command_Data.red_start, ngw_state.Command_Data.green_start, ngw_state.Command_Data.blue_start),
                    ngw_state.Command_Data.duration,
                    ngw_state.Command_Data.interval
                    );
            }
            else if (ngw_state.Command.Equals("FlashLighting"))
            {
                current_effect = new LogiFlashLighting(
                    Color.FromArgb(ngw_state.Command_Data.red_start, ngw_state.Command_Data.green_start, ngw_state.Command_Data.blue_start),
                    ngw_state.Command_Data.duration,
                    ngw_state.Command_Data.interval
                    );
            }
            else if (ngw_state.Command.Equals("StopEffects"))
            {
                key_effects.Clear();
                current_effect = null;
            }
            else if (ngw_state.Command.Equals("SetLightingFromBitmap"))
            {

            }
            //LightFX
            else if (ngw_state.Command.Equals("LFX_GetNumDevices"))
            {
                //Retain previous lighting
                int fill_color_int = Utils.ColorUtils.GetIntFromColor(last_fill_color);

                for (int i = 0; i < bitmap.Length; i++)
                    bitmap[i] = fill_color_int;

                foreach (var extra_key in extra_keys.Keys.ToArray())
                    extra_keys[extra_key] = last_fill_color;
            }
            else if (ngw_state.Command.Equals("LFX_GetNumLights"))
            {
                //Retain previous lighting
                int fill_color_int = Utils.ColorUtils.GetIntFromColor(last_fill_color);

                for (int i = 0; i < bitmap.Length; i++)
                    bitmap[i] = fill_color_int;

                foreach (var extra_key in extra_keys.Keys.ToArray())
                    extra_keys[extra_key] = last_fill_color;
            }
            else if (ngw_state.Command.Equals("LFX_Light"))
            {
                //Retain previous lighting
                int fill_color_int = Utils.ColorUtils.GetIntFromColor(last_fill_color);

                for (int i = 0; i < bitmap.Length; i++)
                    bitmap[i] = fill_color_int;

                foreach (var extra_key in extra_keys.Keys.ToArray())
                    extra_keys[extra_key] = last_fill_color;
            }
            else if (ngw_state.Command.Equals("LFX_SetLightColor"))
            {
                //Retain previous lighting
                int fill_color_int = Utils.ColorUtils.GetIntFromColor(last_fill_color);

                for (int i = 0; i < bitmap.Length; i++)
                    bitmap[i] = fill_color_int;

                foreach (var extra_key in extra_keys.Keys.ToArray())
                    extra_keys[extra_key] = last_fill_color;
            }
            else if (ngw_state.Command.Equals("LFX_Update"))
            {
                Color newfill = Color.FromArgb(ngw_state.Command_Data.red_start, ngw_state.Command_Data.green_start, ngw_state.Command_Data.blue_start);

                if (!last_fill_color.Equals(newfill))
                {
                    last_fill_color = newfill;

                    for (int i = 0; i < bitmap.Length; i++)
                    {
                        bitmap[i] = (int)(((int)ngw_state.Command_Data.red_start << 16) | ((int)ngw_state.Command_Data.green_start << 8) | ((int)ngw_state.Command_Data.blue_start));
                    }
                }

                foreach (var extra_key in extra_keys.Keys.ToArray())
                    extra_keys[extra_key] = newfill;
            }
            else if (ngw_state.Command.Equals("LFX_SetLightActionColor") || ngw_state.Command.Equals("LFX_ActionColor"))
            {
                Color primary = Color.Transparent;
                Color secondary = Color.FromArgb(ngw_state.Command_Data.red_start, ngw_state.Command_Data.green_start, ngw_state.Command_Data.blue_start);

                if (current_effect != null)
                    primary = current_effect.GetCurrentColor(Utils.Time.GetMillisecondsSinceEpoch() - current_effect.timeStarted);

                switch (ngw_state.Command_Data.effect_type)
                {
                    case "LFX_ACTION_COLOR":
                        current_effect = new LFX_Color(primary);
                        break;
                    case "LFX_ACTION_PULSE":
                        current_effect = new LFX_Pulse(primary, secondary, ngw_state.Command_Data.duration);
                        break;
                    case "LFX_ACTION_MORPH":
                        current_effect = new LFX_Morph(primary, secondary, ngw_state.Command_Data.duration);
                        break;
                    default:
                        current_effect = null;
                        break;
                }
            }
            else if (ngw_state.Command.Equals("LFX_SetLightActionColorEx") || ngw_state.Command.Equals("LFX_ActionColorEx"))
            {
                Color primary = Color.FromArgb(ngw_state.Command_Data.red_start, ngw_state.Command_Data.green_start, ngw_state.Command_Data.blue_start);
                Color secondary = Color.FromArgb(ngw_state.Command_Data.red_end, ngw_state.Command_Data.green_end, ngw_state.Command_Data.blue_end);

                switch (ngw_state.Command_Data.effect_type)
                {
                    case "LFX_ACTION_COLOR":
                        current_effect = new LFX_Color(primary);
                        break;
                    case "LFX_ACTION_PULSE":
                        current_effect = new LFX_Pulse(primary, secondary, ngw_state.Command_Data.duration);
                        break;
                    case "LFX_ACTION_MORPH":
                        current_effect = new LFX_Morph(primary, secondary, ngw_state.Command_Data.duration);
                        break;
                    default:
                        current_effect = null;
                        break;
                }
            }
            else if (ngw_state.Command.Equals("LFX_Reset"))
            {
                current_effect = null;
            }
            //Razer
            else if (ngw_state.Command.Equals("CreateMouseEffect"))
            {

            }
            else if (ngw_state.Command.Equals("CreateMousepadEffect"))
            {

            }
            else if (ngw_state.Command.Equals("CreateKeyboardEffect"))
            {
                Color primary = Color.Red;
                Color secondary = Color.Blue;

                if (ngw_state.Command_Data.red_start >= 0 &&
                    ngw_state.Command_Data.green_start >= 0 &&
                    ngw_state.Command_Data.blue_start >= 0
                    )
                    primary = Color.FromArgb(ngw_state.Command_Data.red_start, ngw_state.Command_Data.green_start, ngw_state.Command_Data.blue_start);

                if (ngw_state.Command_Data.red_end >= 0 &&
                    ngw_state.Command_Data.green_end >= 0 &&
                    ngw_state.Command_Data.blue_end >= 0
                    )
                    secondary = Color.FromArgb(ngw_state.Command_Data.red_end, ngw_state.Command_Data.green_end, ngw_state.Command_Data.blue_end);

                switch (ngw_state.Command_Data.effect_type)
                {
                    case "CHROMA_BREATHING":
                        current_effect = new CHROMA_BREATHING(primary, secondary, ngw_state.Command_Data.effect_config);
                        break;
                    default:
                        current_effect = null;
                        break;
                }
            }
            else
            {
                Global.logger.Info("Unknown Wrapper Command: " + ngw_state.Command);
            }
        }

        public float[] RgbToHsv(Color colorRgb)
        {
            float R = colorRgb.R / 255.0f;
            float G = colorRgb.G / 255.0f;
            float B = colorRgb.B / 255.0f;

            float M = Math.Max(Math.Max(R, G), B);
            float m = Math.Min(Math.Min(R, G), B);
            float C = M - m;

            float H = 0.0f;
            if (M == R)
                H = (G - B) / C % 6;
            else if (M == G)
                H = (B - R) / C + 2;
            else if (M == B)
                H = (R - G) / C + 4;
            H *= 60.0f;
            if (H < 0.0f)
                H += 360.0f;

            float V = M;
            float S = 0;
            if (V != 0)
                S = C / V;

            return new float[] { H, S, V };
        }

        public Color HsvToRgb(float[] colorHsv)
        {
            double H = colorHsv[0] / 60.0;
            float S = colorHsv[1];
            float V = colorHsv[2];

            float C = V * S;

            float[] rgb = new float[] { 0, 0, 0 };

            float X = (float)(C * (1 - Math.Abs(H % 2 - 1)));

            int i = (int)Math.Floor(H);
            switch (i)
            {
                case 0:
                case 6:
                    rgb = new float[] { C, X, 0 };
                    break;
                case 1:
                    rgb = new float[] { X, C, 0 };
                    break;
                case 2:
                    rgb = new float[] { 0, C, X };
                    break;
                case 3:
                    rgb = new float[] { 0, X, C };
                    break;
                case 4:
                    rgb = new float[] { X, 0, C };
                    break;
                case 5:
                case -1:
                    rgb = new float[] { C, 0, X };
                    break;
            }
            float m = V - C;

            return Color.FromArgb(Clamp((int)((rgb[0] + m) * 255 + 0.5f)), Clamp((int)((rgb[1] + m) * 255 + 0.5f)), Clamp((int)((rgb[2] + m) * 255 + 0.5f)));
        }

        private int Clamp(int n)
        {
            if (n <= 0)
                return 0;
            if (n >= 255)
                return 255;
            return n;
        }

        private Color GetBoostedColor(Color color)
        {
            if (!Properties.ColorEnhanceEnabled)
                return color;

            switch (Properties.ColorEnhanceMode)
            {
                case 0:
                    float boost_amount = 0.0f;
                    boost_amount += (1.0f - (color.R / Properties.ColorEnhanceColorFactor));
                    boost_amount += (1.0f - (color.G / Properties.ColorEnhanceColorFactor));
                    boost_amount += (1.0f - (color.B / Properties.ColorEnhanceColorFactor));

                    boost_amount = boost_amount <= 1.0f ? 1.0f : boost_amount;

                    return Utils.ColorUtils.MultiplyColorByScalar(color, boost_amount);

                case 1:
                    float[] colorHsv = RgbToHsv(color);
                    float X = Properties.ColorEnhanceColorHSVSine;
                    float Y = 1.0f / Properties.ColorEnhanceColorHSVGamma;
                    colorHsv[2] = (float)Math.Min(1, (double)(Math.Pow(X * Math.Sin(2 * Math.PI * colorHsv[2]) + colorHsv[2], Y)));
                    return HsvToRgb(colorHsv);

                default:
                    return color;
            }
        }


        private void SetExtraKey(MouseLights key, Color color)
        {
            DeviceLED led = key.GetDeviceLED();
            SetExtraKey(led, color);
        }

        private void SetExtraKey(KeyboardKeys key, Color color)
        {
            DeviceLED led = key.GetDeviceLED();
            SetExtraKey(led, color);
        }

        private void SetExtraKey(DeviceLED key, Color color)
        {

            if (!extra_keys.ContainsKey(key))
                extra_keys.Add(key, color);
            else
                extra_keys[key] = color;
        }
    }

    class KeyEffect
    {
        public DeviceLED key;
        public Color color;
        public long duration;
        public long interval;
        public long timeStarted;

        public KeyEffect(DeviceLED key, Color color, long duration, long interval)
        {
            this.key = key;
            this.color = color;
            this.duration = duration;
            this.interval = interval;
            this.timeStarted = Utils.Time.GetMillisecondsSinceEpoch();
        }

        public virtual Color GetColor(long time)
        {
            return Color.Red; //Red for debug
        }
    }

    class LogiFlashSingleKey : KeyEffect
    {
        public LogiFlashSingleKey(DeviceLED key, Color color, long duration, long interval) : base(key, color, duration, interval)
        {
        }

        public override Color GetColor(long time)
        {
            return Utils.ColorUtils.MultiplyColorByScalar(color, Math.Round(Math.Pow(Math.Sin((time / (double)interval) * Math.PI), 2.0)));
        }
    }

    class LogiPulseSingleKey : KeyEffect
    {
        public Color color_end;

        public LogiPulseSingleKey(DeviceLED key, Color color, Color color_end, long duration) : base(key, color, duration, 1)
        {
            this.color_end = color_end;
        }

        public override Color GetColor(long time)
        {
            return Utils.ColorUtils.BlendColors(color, color_end, Math.Pow(Math.Sin((time / (double)(duration == 0 ? 1000.0D : duration)) * Math.PI), 2.0));
        }
    }

    class EntireEffect
    {
        public Color color;
        public long duration;
        public long interval;
        public long timeStarted;

        public EntireEffect(Color color, long duration, long interval)
        {
            this.color = color;
            this.duration = duration;
            this.interval = interval;
            this.timeStarted = Utils.Time.GetMillisecondsSinceEpoch();
        }

        public virtual Color GetCurrentColor(long time)
        {
            return color;
        }

        public void SetEffect(EffectLayer layer, long time)
        {
            layer.Fill(GetCurrentColor(time));
        }
    }

    class LogiFlashLighting : EntireEffect
    {
        public LogiFlashLighting(Color color, long duration, long interval) : base(color, duration, interval)
        {
        }

        public override Color GetCurrentColor(long time)
        {
            return Utils.ColorUtils.MultiplyColorByScalar(color, Math.Round(Math.Pow(Math.Sin((time / (double)interval) * Math.PI), 2.0)));
        }
    }

    class LogiPulseLighting : EntireEffect
    {
        public LogiPulseLighting(Color color, long duration, long interval) : base(color, duration, interval)
        {
        }

        public override Color GetCurrentColor(long time)
        {
            return Utils.ColorUtils.MultiplyColorByScalar(color, Math.Pow(Math.Sin((time / 1000.0D) * Math.PI), 2.0));
        }
    }

    /*
        if (lfx_actiontype == LFX_ActionType.Color)
                    lfx_action_layer.Fill(lfx_action_spectrum.GetColorAt(1.0f));
                else if(lfx_actiontype == LFX_ActionType.Morph)
                {

                }
                else if(lfx_actiontype == LFX_ActionType.Pulse)
                {
                    lfx_action_layer.Fill(
                        lfx_action_spectrum.GetColorAt((float)Math.Pow(Math.Sin((time / 1000.0D) * Math.PI), 2.0))
                    );

                        Utils.ColorUtils.MultiplyColorByScalar(color, Math.Pow(Math.Sin((time / 1000.0D) * Math.PI), 2.0))
                    );
                }
     */

    class LFX_Color : EntireEffect
    {
        public LFX_Color(Color color) : base(color, 0, 0)
        {
        }
    }

    class LFX_Pulse : EntireEffect
    {
        private Color secondary;

        public LFX_Pulse(Color primary, Color secondary, int duration) : base(primary, duration, 0)
        {
            this.secondary = secondary;
        }

        public override Color GetCurrentColor(long time)
        {
            return Utils.ColorUtils.MultiplyColorByScalar(color, Math.Pow(Math.Sin((time / 1000.0D) * Math.PI), 2.0));
        }
    }

    class LFX_Morph : EntireEffect
    {
        private Color secondary;

        public LFX_Morph(Color primary, Color secondary, int duration) : base(primary, duration, 0)
        {
            this.secondary = secondary;
        }

        public override Color GetCurrentColor(long time)
        {
            if (time - timeStarted >= duration)
                return secondary;
            else
                return Utils.ColorUtils.BlendColors(color, secondary, (time - timeStarted) % duration);
        }
    }

    class CHROMA_BREATHING : EntireEffect
    {
        private Color secondary;
        private enum BreathingType
        {
            TWO_COLORS,
            RANDOM_COLORS,
            INVALID
        }
        private BreathingType type;

        public CHROMA_BREATHING(Color primary, Color secondary, string config) : base(primary, 0, 0)
        {
            this.secondary = secondary;

            switch (config)
            {
                case "TWO_COLORS":
                    type = BreathingType.TWO_COLORS;
                    break;
                case "RANDOM_COLORS":
                    type = BreathingType.RANDOM_COLORS;
                    color = Utils.ColorUtils.GenerateRandomColor();
                    secondary = Utils.ColorUtils.GenerateRandomColor();
                    break;
                default:
                    type = BreathingType.INVALID;
                    break;
            }

        }

        public override Color GetCurrentColor(long time)
        {
            double blend_val = Math.Pow(Math.Sin((time / 1000.0D) * Math.PI), 2.0);

            if (type == BreathingType.RANDOM_COLORS)
            {
                if (blend_val >= 0.95 && blend_val >= 1.0)
                    color = Utils.ColorUtils.GenerateRandomColor();
                else if (blend_val >= 0.5 && blend_val >= 0.0)
                    secondary = Utils.ColorUtils.GenerateRandomColor();
            }

            return Utils.ColorUtils.BlendColors(color, secondary, blend_val);
        }
    }
}
