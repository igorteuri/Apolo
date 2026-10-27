/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 Emojis for the buttons. The pictures are the colored emojis of "Noto Emoji" (Google, Apache License 2.0), with the
 "Twemoji" set as a second source. They are downloaded once, when an emoji is chosen, and kept in %APPDATA%\Apolo\emoji-cache.
 The catalog has the emojis that fit a tabletop RPG, each with words (Portuguese and English) used to find it and to guess
 the emoji of a button from its title.
*/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Ares.Player
{
    internal class EmojiEntry
    {
        public string Code;          // hex code point(s), lower case, joined with "-" (e.g. "1f332")
        public string[] Words;       // folded (no accents), lower case
        public string Label;         // first word, for the tooltip

        public string Glyph
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                foreach (string part in Code.Split('-'))
                    sb.Append(char.ConvertFromUtf32(int.Parse(part, NumberStyles.HexNumber)));
                return sb.ToString();
            }
        }
    }

    internal static class EmojiCatalog
    {
        // code | words (portuguese, english)
        private static readonly string[] s_Data =
        {
            // nature and weather
            "1f332|floresta,bosque,pinheiro,mata,forest,woods,pine",
            "1f333|arvore,carvalho,tree,oak",
            "1f334|palmeira,tropical,ilha,selva,island,palm,jungle",
            "1f340|trevo,sorte,clover,luck",
            "1f33f|erva,ervas,planta,herb,plant",
            "1f344|cogumelo,fungo,mushroom",
            "1f338|flor,flores,primavera,flower,spring",
            "1f30a|mar,oceano,onda,ondas,maritimo,sea,ocean,wave",
            "1f3d4|montanha,montanhas,pico,serra,mountain",
            "1f30b|vulcao,lava,volcano",
            "1f3dc|deserto,dunas,areia,desert,sand",
            "2744|neve,gelo,inverno,frio,snow,ice,winter,cold",
            "1f525|fogo,chama,incendio,fogueira,fire,flame,burn",
            "1f4a7|agua,gota,rio,lago,water,river,lake",
            "26a1|raio,relampago,tempestade,trovao,storm,lightning,thunder",
            "1f327|chuva,garoa,rain",
            "1f319|lua,noite,noturno,moon,night",
            "1f31e|sol,dia,ensolarado,sun,day",
            "2b50|estrela,estrelas,star",
            "1f31f|heroi,epico,gloria,destino,hero,epic,glory,destiny",
            "1f30c|galaxia,espaco,cosmos,galaxy,space",
            "1f4a8|vento,wind",
            "1f30d|mundo,terra,world,earth",
            // places
            "1f3f0|castelo,reino,fortaleza,castle,kingdom,fortress",
            "1f3e0|casa,lar,house,home",
            "1f3e1|vila,aldeia,village",
            "26ea|igreja,templo,catedral,church,temple,cathedral",
            "1f37a|taverna,bar,cerveja,estalagem,pousada,tavern,inn,pub,beer",
            "26fa|acampamento,tenda,camp,tent,camping",
            "1f573|caverna,buraco,cave,hole",
            "1f306|cidade,urbano,city,urban",
            "1f3ed|fabrica,industria,factory,industry",
            "1f6a2|navio,porto,ship,port,cruzeiro",
            "26f5|barco,veleiro,boat,sail",
            "2693|ancora,anchor",
            "1f3f4|pirata,bandeira,pirate,flag",
            "1f5ff|estatua,ruina,ruinas,antigo,antiga,statue,ruins,ancient",
            "1f3fa|vaso,antiguidade,grego,greco,roma,romano,egito,egipcio,greek,roman,egypt,vase",
            "1f6aa|porta,entrada,door,entrance",
            "1f300|portal,vortice,teletransporte,dimensao,ciclone,vortex,teleport,dimension",
            // creatures
            "1f409|dragao,dragoes,dragon",
            "1f47b|fantasma,assombrado,espirito,assombracao,ghost,haunted,spirit",
            "1f480|caveira,morte,esqueleto,cripta,morto,skull,death,skeleton,crypt,dead",
            "1f9df|zumbi,zumbis,zombie",
            "1f9db|vampiro,vampire",
            "1f9d9|mago,maga,bruxa,bruxo,feiticeiro,feiticeira,magia,magico,wizard,mage,witch,magic,sorcerer",
            "1f9da|fada,fae,fairy",
            "1f9dd|elfo,elfos,elf,elves",
            "1f9de|genio,djinn,genie",
            "1f9cc|troll,golem,gigante,giant",
            "1f47e|alien,alienigena,invasor,invader",
            "1f47f|demonio,demonios,diabo,inferno,infernal,demon,devil,hell",
            "1f47c|anjo,celestial,divino,ceu,angel,divine,heaven",
            "1f479|ogro,oni,ogre",
            "1f47a|goblin,goblins,duende",
            "1f43a|lobo,matilha,lobisomem,wolf,werewolf,pack",
            "1f40d|cobra,serpente,snake,serpent",
            "1f577|aranha,spider",
            "1f987|morcego,bat",
            "1f985|aguia,gaviao,eagle,hawk",
            "1f989|coruja,owl",
            "1f40e|cavalo,cavalaria,horse,cavalry",
            "1f419|polvo,kraken,tentaculo,octopus,tentacle",
            "1f988|tubarao,shark",
            "1f43b|urso,bear",
            "1f400|rato,ratos,esgoto,rat,sewer",
            "1f982|escorpiao,scorpion",
            "1f417|javali,porco,boar,pig",
            "1f98c|cervo,veado,deer",
            "1f984|unicornio,unicorn",
            "1f40a|crocodilo,jacare,pantano,crocodile,alligator,swamp",
            "1f41d|abelha,bee",
            "1f43e|pata,rastro,cacada,caca,paw,track,hunt,hunting",
            "1f9b4|osso,ossos,bone,bones",
            // items
            "2694|batalha,luta,combate,guerra,duelo,espada,battle,fight,combat,war,duel,sword",
            "1f5e1|adaga,faca,punhal,dagger,knife",
            "1f3f9|arco,flecha,arqueiro,bow,arrow,archer",
            "1f6e1|escudo,defesa,shield,defense",
            "1fa93|machado,axe",
            "1f528|martelo,ferreiro,forja,hammer,blacksmith,forge",
            "1f451|coroa,rei,rainha,realeza,crown,king,queen,royal",
            "1f48e|gema,joia,diamante,gem,jewel,diamond",
            "1f4b0|tesouro,dinheiro,ouro,saque,treasure,gold,money,loot",
            "1f511|chave,key",
            "1f512|trancado,cadeado,prisao,locked,lock,prison",
            "1f4dc|pergaminho,carta,scroll,letter",
            "1f5fa|mapa,viagem,explorar,map,travel,explore",
            "1f4d6|livro,biblioteca,grimorio,historia,lore,book,library,grimoire,story",
            "1f52e|cristal,profecia,oraculo,vidente,crystal,prophecy,oracle",
            "1f9ff|amuleto,talisma,amulet,talisman",
            "1f526|lanterna,tocha,luz,torch,lantern,light",
            "1fa84|varinha,wand",
            "1f9ea|alquimia,pocao,veneno,laboratorio,alchemy,potion,poison,lab",
            "1f9ed|bussola,navegacao,compass,navigation",
            "23f3|tempo,espera,ampulheta,time,wait,hourglass",
            "1f514|sino,alarme,alerta,aviso,bell,alarm,alert",
            "1f3af|alvo,missao,objetivo,busca,target,mission,goal,quest",
            "2696|justica,tribunal,julgamento,balanca,justice,court,trial",
            "2695|cura,curandeiro,medico,hospital,heal,healing,doctor",
            "1f3b2|dado,dados,jogo,dice,game",
            // music and sound
            "1f3ba|trombeta,fanfarra,corneta,arauto,trumpet,fanfare,herald",
            "1f3b8|guitarra,rock,metal,guitar",
            "1f3bb|violino,celta,celtico,folk,violin,celtic",
            "1f941|tambor,tambores,ritmo,tribal,drum,drums,rhythm",
            "1f3b9|piano,teclado,keyboard",
            "1f3a4|cantar,canto,voz,bardo,sing,voice,bard",
            "1f3b5|musica,melodia,trilha,tema,music,melody,soundtrack,theme",
            "1f50a|som,volume,audio,ambiente,ambiencia,sound,ambience,ambient",
            "1f3ad|drama,teatro,mascara,intriga,disfarce,theater,mask,intrigue",
            "1f3ac|cinema,cena,filme,abertura,intro,abertura,scene,movie,opening",
            // feelings and situations
            "1f634|dormir,sono,descanso,calmo,calma,sleep,rest,calm",
            "1f389|festa,celebracao,comemoracao,party,celebration",
            "1f3c6|vitoria,vencer,trofeu,campeao,victory,win,trophy,champion",
            "1f622|triste,tristeza,melancolia,luto,funeral,sad,sorrow,mourning",
            "1f631|medo,horror,terror,susto,suspense,fear,scary",
            "1f620|raiva,furia,ira,anger,angry,fury,rage",
            "1f496|amor,romance,romantico,coracao,love,heart",
            "1f4ac|conversa,dialogo,fofoca,talk,conversation,dialog",
            "1f6b6|caminhada,estrada,caminho,jornada,travessia,walk,road,path,journey",
            "1f3c3|correr,fuga,fugir,corrida,run,escape,chase",
            "1f575|investigacao,detetive,misterio,crime,investigation,detective,mystery",
            "1f46e|guarda,policia,guard,police",
            "1f3a3|pescar,pesca,fishing",
            "1f356|comida,banquete,carne,cozinha,refeicao,food,feast,meat,kitchen",
            "1f377|vinho,bebida,wine,drink",
            "2728|brilho,encantamento,feitico,sparkle,enchant,spell,enchantment",
            "1f4a3|bomba,explosao,explosivo,bomb,explosion",
            "1f4a5|impacto,colisao,boom,crash",
            "1f52b|tiro,pistola,tiroteio,gun,pistol,shot",
            // sci-fi and modern
            "1f680|foguete,nave,ficcao,cyber,futuro,rocket,spaceship,scifi,future",
            "1f916|robo,maquina,android,robot,machine",
            "1f6f8|ovni,disco voador,ufo",
            "1f697|carro,perseguicao,veiculo,car,vehicle"
        };

        private static List<EmojiEntry> s_All;

        public static List<EmojiEntry> All
        {
            get
            {
                if (s_All == null)
                {
                    List<EmojiEntry> list = new List<EmojiEntry>();
                    foreach (string line in s_Data)
                    {
                        string[] parts = line.Split('|');
                        EmojiEntry e = new EmojiEntry();
                        e.Code = parts[0];
                        e.Words = parts[1].Split(',');
                        e.Label = e.Words[0];
                        list.Add(e);
                    }
                    s_All = list;
                }
                return s_All;
            }
        }

        public static string Fold(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            string d = text.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();
            foreach (char c in d)
            {
                UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat != UnicodeCategory.NonSpacingMark)
                    sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        // ---- the big dictionary: all emojis of the Unicode CLDR annotations (Portuguese and English) ----

        private static volatile List<EmojiEntry> s_Extended;
        private static int s_ExtendedStarted;

        public static bool ExtendedReady { get { return s_Extended != null; } }

        private static string AnnotationFile(string lang)
        {
            return Path.Combine(CacheFolder, "annotations-" + lang + ".json");
        }

        private static string LoadAnnotationText(string lang)
        {
            string file = AnnotationFile(lang);
            try
            {
                if (File.Exists(file) && new FileInfo(file).Length > 1000)
                    return File.ReadAllText(file, Encoding.UTF8);
                byte[] data = OpenMedia.GetBytes("https://raw.githubusercontent.com/unicode-org/cldr-json/main/cldr-json/cldr-annotations-full/annotations/"
                    + lang + "/annotations.json");
                if (data == null || data.Length < 1000)
                    return null;
                Directory.CreateDirectory(CacheFolder);
                File.WriteAllBytes(file, data);
                return Encoding.UTF8.GetString(data);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // glyph -> code ("1f332"); null for sequences that are not used (flags, skin tones, joined emojis, keycaps)
        private static string CodeOf(string glyph)
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < glyph.Length; i++)
            {
                int cp;
                if (char.IsHighSurrogate(glyph[i]) && i + 1 < glyph.Length)
                {
                    cp = char.ConvertToUtf32(glyph[i], glyph[i + 1]);
                    i++;
                }
                else
                {
                    cp = glyph[i];
                }
                if (cp == 0xfe0f)
                    continue;
                if (cp == 0x200d || cp == 0x20e3 || (cp >= 0x1f3fb && cp <= 0x1f3ff) || (cp >= 0x1f1e6 && cp <= 0x1f1ff) || cp >= 0xe0000 || cp < 0x80)
                    return null;
                parts.Add(cp.ToString("x"));
            }
            return parts.Count == 0 ? null : string.Join("-", parts.ToArray());
        }

        private static void AddAnnotations(string json, Dictionary<string, EmojiEntry> map, bool primary)
        {
            if (string.IsNullOrEmpty(json))
                return;
            System.Web.Script.Serialization.JavaScriptSerializer js = new System.Web.Script.Serialization.JavaScriptSerializer();
            js.MaxJsonLength = int.MaxValue;
            Dictionary<string, object> root = js.DeserializeObject(json) as Dictionary<string, object>;
            object a1;
            if (root == null || !root.TryGetValue("annotations", out a1))
                return;
            Dictionary<string, object> level1 = a1 as Dictionary<string, object>;
            object a2;
            if (level1 == null || !level1.TryGetValue("annotations", out a2))
                return;
            Dictionary<string, object> items = a2 as Dictionary<string, object>;
            if (items == null)
                return;
            foreach (KeyValuePair<string, object> pair in items)
            {
                string code = CodeOf(pair.Key);
                if (code == null)
                    continue;
                Dictionary<string, object> info = pair.Value as Dictionary<string, object>;
                if (info == null)
                    continue;
                EmojiEntry entry;
                if (!map.TryGetValue(code, out entry))
                {
                    entry = new EmojiEntry();
                    entry.Code = code;
                    entry.Words = new string[0];
                    entry.Label = "";
                    map[code] = entry;
                }
                List<string> words = new List<string>(entry.Words);
                foreach (string key in new[] { "tts", "default" })
                {
                    object list;
                    if (!info.TryGetValue(key, out list))
                        continue;
                    object[] arr = list as object[];
                    if (arr == null)
                    {
                        System.Collections.ArrayList al = list as System.Collections.ArrayList;
                        arr = al != null ? al.ToArray() : null;
                    }
                    if (arr == null)
                        continue;
                    foreach (object o in arr)
                    {
                        string text = Convert.ToString(o);
                        if (key == "tts" && primary && entry.Label.Length == 0)
                            entry.Label = text;
                        string folded = Fold(text);
                        if (folded.Length >= 3 && !words.Contains(folded))
                            words.Add(folded);
                        foreach (string w in folded.Split(' '))
                        {
                            if (w.Length >= 4 && !words.Contains(w))
                                words.Add(w);
                        }
                    }
                }
                entry.Words = words.ToArray();
            }
        }

        /// <summary>Loads (and, the first time, downloads) the big dictionary in the background; "ready" is called from a background thread.</summary>
        public static void PrepareExtendedAsync(Action ready)
        {
            if (s_Extended != null)
            {
                if (ready != null)
                    ready();
                return;
            }
            if (System.Threading.Interlocked.Exchange(ref s_ExtendedStarted, 1) != 0)
            {
                // somebody else is loading: wait for the result
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    for (int i = 0; i < 300 && s_Extended == null && s_ExtendedStarted == 1; i++)
                        System.Threading.Thread.Sleep(200);
                    if (s_Extended != null && ready != null)
                        ready();
                });
                return;
            }
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    Dictionary<string, EmojiEntry> map = new Dictionary<string, EmojiEntry>();
                    string pt = LoadAnnotationText("pt");
                    string en = LoadAnnotationText("en");
                    AddAnnotations(pt, map, true);
                    AddAnnotations(en, map, pt == null);
                    List<EmojiEntry> list = new List<EmojiEntry>();
                    foreach (EmojiEntry e in map.Values)
                    {
                        if (Find(e.Code) != null || e.Words.Length == 0)
                            continue;
                        if (e.Label.Length == 0)
                            e.Label = e.Words[0];
                        list.Add(e);
                    }
                    if (list.Count > 0)
                        s_Extended = list;
                }
                catch (Exception swallowed)
                {
                    Ares.Settings.ErrorLog.Note(swallowed);
                }
                finally
                {
                    if (s_Extended == null)
                        s_ExtendedStarted = 0;   // try again next time
                }
                if (s_Extended != null && ready != null)
                    ready();
            });
        }

        /// <summary>The emojis that fit a search text: the curated ones first, then (when loaded) any other emoji.</summary>
        public static List<EmojiEntry> Search(string query)
        {
            string q = Fold(query).Trim();
            List<EmojiEntry> result = new List<EmojiEntry>();
            foreach (EmojiEntry e in All)
            {
                if (q.Length == 0)
                {
                    result.Add(e);
                    continue;
                }
                foreach (string w in e.Words)
                {
                    if (w.IndexOf(q, StringComparison.Ordinal) >= 0)
                    {
                        result.Add(e);
                        break;
                    }
                }
            }
            List<EmojiEntry> ext = s_Extended;
            if (ext != null && q.Length >= 3)
            {
                int extra = 0;
                foreach (EmojiEntry e in ext)
                {
                    foreach (string w in e.Words)
                    {
                        if (w.IndexOf(q, StringComparison.Ordinal) >= 0)
                        {
                            result.Add(e);
                            extra++;
                            break;
                        }
                    }
                    if (extra >= 80)
                        break;
                }
            }
            return result;
        }

        private static void Score(EmojiEntry e, string t, string[] words, int bonus, bool exactOnly, ref EmojiEntry best, ref int bestScore)
        {
            foreach (string kw in e.Words)
            {
                if (kw.Length < 3)
                    continue;
                int score = 0;
                foreach (string w in words)
                {
                    if (w == kw)
                        score = Math.Max(score, 100 + kw.Length);
                    else if (!exactOnly && kw.Length >= 4 && w.Length >= 4 && (w.StartsWith(kw, StringComparison.Ordinal) || kw.StartsWith(w, StringComparison.Ordinal)))
                        score = Math.Max(score, 50 + Math.Min(kw.Length, w.Length));
                }
                if (kw.IndexOf(' ') >= 0 && t.IndexOf(kw, StringComparison.Ordinal) >= 0)
                    score = Math.Max(score, 120 + kw.Length);
                if (score > 0)
                    score += bonus;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = e;
                }
            }
        }

        /// <summary>Guesses the emoji for the title of a button; null when nothing fits.</summary>
        public static EmojiEntry Match(string title)
        {
            string t = Fold(title);
            if (t.Length == 0)
                return null;
            string[] words = t.Split(new[] { ' ', '-', '_', ',', '.', ':', ';', '/', '(', ')', '[', ']', '&', '\'' }, StringSplitOptions.RemoveEmptyEntries);
            EmojiEntry best = null;
            int bestScore = 0;
            foreach (EmojiEntry e in All)
                Score(e, t, words, 25, false, ref best, ref bestScore);
            List<EmojiEntry> ext = s_Extended;
            if (ext != null)
            {
                // the big dictionary has many loose words: only whole-word matches count
                foreach (EmojiEntry e in ext)
                    Score(e, t, words, 0, true, ref best, ref bestScore);
            }
            return best;
        }

        public static EmojiEntry Find(string code)
        {
            foreach (EmojiEntry e in All)
            {
                if (e.Code == code)
                    return e;
            }
            return null;
        }

        // ---- the pictures ----

        public static string CacheFolder
        {
            get { return Path.Combine(Ares.Settings.AppFolders.Root, "emoji-cache"); }
        }

        public static string CachedPath(string code)
        {
            return Path.Combine(CacheFolder, code + ".png");
        }

        /// <summary>The file of the picture of an emoji; it is downloaded the first time. Null if it could not be loaded.</summary>
        public static string GetPicture(string code)
        {
            string file = CachedPath(code);
            try
            {
                if (File.Exists(file) && new FileInfo(file).Length > 100)
                    return file;
                Directory.CreateDirectory(CacheFolder);
                string[] urls =
                {
                    "https://raw.githubusercontent.com/googlefonts/noto-emoji/main/png/128/emoji_u" + code.Replace('-', '_') + ".png",
                    "https://cdn.jsdelivr.net/gh/jdecked/twemoji@15.1.0/assets/72x72/" + code + ".png"
                };
                foreach (string url in urls)
                {
                    try
                    {
                        byte[] data = OpenMedia.GetBytes(url);
                        if (data != null && data.Length > 100)
                        {
                            File.WriteAllBytes(file, data);
                            return file;
                        }
                    }
                    catch (Exception swallowed)
                    {
                        Ares.Settings.ErrorLog.Note(swallowed);
                    }
                }
            }
            catch (Exception swallowed)
            {
                Ares.Settings.ErrorLog.Note(swallowed);
            }
            return null;
        }
    }
}
