/*
 Copyright (c) 2015 [Joerg Ruedenauer]
 
 This file is part of Ares.

 Ares is free software; you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation; either version 2 of the License, or
 (at your option) any later version.

 Ares is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with Ares; if not, write to the Free Software
 Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
 */
using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace Ares.CommonGUI
{
    public partial class AboutDialog : Form
    {
        /// <summary>Link of the author's channel (Dados Críticos).</summary>
        public const string ChannelUrl = "https://igorteuri.itch.io/";

        public AboutDialog()
        {
            InitializeComponent();
            bool pt = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt";
            string assemblyVersion = (new System.Reflection.AssemblyName(System.Reflection.Assembly.GetExecutingAssembly().FullName)).Version.ToString();

            // the old designer layout is replaced by a simple modern one
            Controls.Clear();
            Text = pt ? "Sobre o Apolo" : "About Apolo";
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(520, 480);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            PictureBox logo = new PictureBox();
            logo.Image = ApoloBrand.Render(96, ApoloState.Playing, 0);
            logo.SizeMode = PictureBoxSizeMode.CenterImage;
            logo.SetBounds(24, 20, 100, 100);
            Controls.Add(logo);

            Label title = new Label();
            title.Text = "Apolo";
            title.Font = new Font(Font.FontFamily, 24f, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(136, 26);
            Controls.Add(title);

            Label sub = new Label();
            sub.Text = (pt ? "Sistema de experiência sonora para RPG" : "Sound experience system for tabletop RPG") + "\r\n"
                + (pt ? "Versão " : "Version ") + assemblyVersion;
            sub.AutoSize = true;
            sub.Location = new Point(139, 74);
            Controls.Add(sub);

            TextBox credits = new TextBox();
            credits.Multiline = true;
            credits.ReadOnly = true;
            credits.ScrollBars = ScrollBars.Vertical;
            credits.BorderStyle = BorderStyle.FixedSingle;
            credits.TabStop = false;
            credits.Text = pt
                ? "O Apolo foi criado por Igor Teuri, do canal Dados Críticos, como uma modernização do Ares.\r\n\r\nO Apolo é um fork do Ares, criado por Jörg Ruedenauer e colaboradores, e é distribuído sob a licença GNU GPL (versão 2 ou posterior), como o projeto original. Todo o crédito pela base do programa pertence ao Ares e a seu autor.\r\n\r\n"
                  + "Áudio: BASS, da un4seen developments (www.un4seen.com). A BASS é gratuita apenas para uso não comercial; para uso comercial é necessária uma licença própria da un4seen.\r\n\r\n"
                  + "Ícones: Lucide (licença ISC). Páginas web embutidas: Microsoft WebView2."
                : "Apolo was created by Igor Teuri, of the Dados Críticos channel, as a modernization of Ares.\r\n\r\nApolo is a fork of Ares, created by Jörg Ruedenauer and contributors, and is distributed under the GNU GPL (version 2 or later), like the original project. All credit for the foundation of this program belongs to Ares and its author.\r\n\r\n"
                  + "Audio: BASS by un4seen developments (www.un4seen.com). BASS is free for non-commercial use only; commercial use requires a license from un4seen.\r\n\r\n"
                  + "Icons: Lucide (ISC license). Embedded web pages: Microsoft WebView2.";
            credits.SetBounds(24, 140, 472, 230);
            Controls.Add(credits);

            LinkLabel channel = new LinkLabel();
            channel.Text = pt ? "Apolo por Igor Teuri — canal Dados Críticos" : "Apolo by Igor Teuri — Dados Críticos channel";
            channel.AutoSize = true;
            channel.Location = new Point(24, 382);
            channel.LinkClicked += (s, e) =>
            {
                try { System.Diagnostics.Process.Start(ChannelUrl); } catch (Exception) { }
            };
            Controls.Add(channel);

            LinkLabel original = new LinkLabel();
            original.Text = pt ? "Projeto original: Ares, de Jörg Ruedenauer" : "Original project: Ares, by Jörg Ruedenauer";
            original.AutoSize = true;
            original.Location = new Point(24, 406);
            original.LinkClicked += (s, e) =>
            {
                try { System.Diagnostics.Process.Start("http://aresrpg.sourceforge.net/"); } catch (Exception) { }
            };
            Controls.Add(original);

            Button ok = new Button();
            ok.Text = "OK";
            ok.DialogResult = DialogResult.OK;
            ok.SetBounds(496 - 100, 434, 100, 30);
            Icon = ApoloBrand.GetIcon(32, ApoloState.Playing, 0);
            Controls.Add(ok);
            AcceptButton = ok;
            CancelButton = ok;
        }
    }
}
