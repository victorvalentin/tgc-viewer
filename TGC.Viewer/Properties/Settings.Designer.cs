﻿//------------------------------------------------------------------------------
// <auto-generated>
//     Este código fue generado por una herramienta.
//     Versión de runtime:4.0.30319.42000
//
//     Los cambios en este archivo podrían causar un comportamiento incorrecto y se perderán si
//     se vuelve a generar el código.
// </auto-generated>
//------------------------------------------------------------------------------

namespace TGC.Viewer.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "15.3.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("..\\..\\..\\TGC.Examples\\Shaders\\")]
        public string ShadersDirectory {
            get {
                return ((string)(this["ShadersDirectory"]));
            }
            set {
                this["ShadersDirectory"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Media\\")]
        public string MediaDirectory {
            get {
                return ((string)(this["MediaDirectory"]));
            }
            set {
                this["MediaDirectory"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Others")]
        public string DefaultExampleCategory {
            get {
                return ((string)(this["DefaultExampleCategory"]));
            }
            set {
                this["DefaultExampleCategory"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Logo de TGC")]
        public string DefaultExampleName {
            get {
                return ((string)(this["DefaultExampleName"]));
            }
            set {
                this["DefaultExampleName"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("TGC Viewer - Técnicas de Gráficos por Computadora - UTN - FRBA")]
        public string Title {
            get {
                return ((string)(this["Title"]));
            }
            set {
                this["Title"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("TgcViewer\\")]
        public string CommonShaders {
            get {
                return ((string)(this["CommonShaders"]));
            }
            set {
                this["CommonShaders"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool ModifiersPanel {
            get {
                return ((bool)(this["ModifiersPanel"]));
            }
            set {
                this["ModifiersPanel"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("https://drive.google.com/open?id=0B8iAMXTVXrJeOGpIbWhUbjJPaE0")]
        public string MediaLink {
            get {
                return ((string)(this["MediaLink"]));
            }
            set {
                this["MediaLink"] = value;
            }
        }
    }
}
