using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PasswordManager
{
    /// <summary>
    /// Generates memorable passphrases from a fixed word list (EFF-style short words).
    /// </summary>
    public static class PassphraseGenerator
    {
        public const int DefaultWordCount = 4;
        public const int MinWordCount = 3;
        public const int MaxWordCount = 10;

        // Compact word list — common short English words suitable for passphrases.
        private static readonly string[] Words =
        {
            "able", "acid", "acre", "act", "add", "age", "ago", "aid", "aim", "air",
            "ale", "all", "aloe", "also", "alto", "amber", "amp", "angel", "angle", "ankle",
            "apple", "april", "arch", "area", "armor", "army", "arrow", "art", "ash", "ask",
            "atom", "aunt", "auto", "autumn", "avenue", "away", "axis", "bacon", "badge", "bag",
            "bait", "baker", "ball", "bamboo", "band", "bank", "bar", "barn", "base", "basin",
            "basket", "bat", "beach", "bean", "bear", "beast", "beat", "bed", "bee", "beet",
            "bell", "belt", "bench", "berry", "best", "bike", "bind", "bird", "birth", "bite",
            "black", "blade", "blame", "blank", "blast", "blend", "bless", "blimp", "blind", "bliss",
            "block", "blood", "bloom", "blow", "blue", "bluff", "blush", "board", "boat", "body",
            "boil", "bold", "bolt", "bond", "bone", "book", "boost", "boot", "bore", "born",
            "boss", "both", "bottle", "bottom", "bounce", "bowl", "box", "boy", "brain", "brake",
            "branch", "brand", "brass", "brave", "bread", "break", "breed", "breeze", "brick", "bridge",
            "brief", "bright", "bring", "brisk", "broad", "brook", "broom", "brother", "brown", "brush",
            "bubble", "buck", "buddy", "budget", "buff", "build", "bulb", "bulk", "bull", "bump",
            "bunch", "bunny", "burn", "burst", "bus", "bush", "busy", "butter", "button", "buyer",
            "cabin", "cable", "cactus", "cage", "cake", "calf", "call", "calm", "camel", "camera",
            "camp", "can", "canal", "candy", "cane", "cannon", "canoe", "canvas", "cape", "carbon",
            "card", "care", "cargo", "carpet", "carrot", "carry", "cart", "case", "cash", "cast",
            "cat", "catch", "cattle", "cause", "cave", "ceiling", "cell", "cement", "cent", "center",
            "chain", "chair", "chalk", "champ", "chance", "change", "chaos", "charge", "charm", "chart",
            "chase", "cheap", "check", "cheek", "cheese", "chef", "cherry", "chess", "chest", "chew",
            "chick", "chief", "child", "chili", "chill", "chimney", "chin", "chip", "choice", "choir",
            "choke", "choose", "chop", "chord", "chore", "chorus", "chose", "chunk", "church", "cider",
            "cigar", "cinema", "circle", "circus", "city", "civic", "civil", "claim", "clamp", "clap",
            "clarify", "claw", "clay", "clean", "clear", "clerk", "clever", "click", "client", "cliff",
            "climb", "cling", "clip", "clock", "clone", "close", "cloth", "cloud", "clown", "club",
            "clue", "clump", "clutch", "coach", "coal", "coast", "coat", "cobra", "cocoa", "code",
            "coffee", "coil", "coin", "cold", "collar", "colony", "color", "column", "comb", "come",
            "comedy", "comfort", "comic", "comma", "common", "company", "compass", "complex", "compute", "concept",
            "concert", "condor", "cone", "confirm", "connect", "cook", "cool", "copper", "copy", "coral",
            "core", "cork", "corn", "corner", "correct", "cost", "cotton", "couch", "cough", "could",
            "count", "country", "couple", "course", "court", "cousin", "cover", "coyote", "crack", "cradle",
            "craft", "cram", "crane", "crash", "crater", "crawl", "crazy", "cream", "credit", "creek",
            "crew", "cricket", "crime", "crisp", "critic", "crop", "cross", "crowd", "crown", "crude",
            "cruel", "cruise", "crumble", "crush", "crust", "cry", "crystal", "cube", "cucumber", "cuff",
            "culture", "cup", "curb", "cure", "curious", "curl", "current", "curry", "curve", "cushion",
            "custom", "cute", "cycle", "dad", "daffodil", "daily", "dairy", "daisy", "dam", "damage",
            "damp", "dance", "daring", "dark", "dart", "dash", "data", "date", "daughter", "dawn",
            "day", "deal", "debate", "debt", "decade", "decide", "deck", "deer", "delay", "deliver",
            "delta", "demand", "denim", "dense", "dent", "dentist", "deny", "depot", "depth", "deputy",
            "derive", "desk", "detail", "detect", "device", "devote", "diagram", "dial", "diamond", "diary",
            "dice", "diesel", "diet", "differ", "digital", "dignity", "dilemma", "dinner", "dinosaur", "direct",
            "dirt", "disagree", "discover", "disease", "dish", "dismiss", "disorder", "display", "distance", "divert",
            "divide", "divorce", "dizzy", "doctor", "document", "dog", "doll", "dolphin", "domain", "donate",
            "donkey", "donor", "door", "dose", "double", "dove", "draft", "dragon", "drama", "drastic",
            "draw", "dream", "dress", "drift", "drill", "drink", "drip", "drive", "drop", "drum",
            "dry", "duck", "dumb", "dune", "during", "dust", "dutch", "duty", "dwarf", "dynamic",
            "eagle", "early", "earn", "earth", "easily", "east", "easy", "echo", "ecology", "economy",
            "edge", "edit", "educate", "effort", "egg", "eight", "either", "elbow", "elder", "electric",
            "elegant", "element", "elephant", "elevator", "elite", "else", "embark", "embody", "embrace", "emerge",
            "emotion", "employ", "empty", "enable", "enact", "end", "endless", "endorse", "enemy", "energy",
            "enforce", "engage", "engine", "enhance", "enjoy", "enlist", "enough", "enrich", "enroll", "ensure",
            "enter", "entire", "entry", "envelope", "episode", "equal", "equip", "era", "erase", "erode",
            "erosion", "error", "erupt", "escape", "essay", "essence", "estate", "eternal", "ethics", "evidence",
            "evil", "evoke", "evolve", "exact", "example", "exceed", "excel", "except", "exchange", "excite",
            "exclude", "excuse", "execute", "exercise", "exhaust", "exhibit", "exile", "exist", "exit", "exotic",
            "expand", "expect", "expire", "explain", "expose", "express", "extend", "extra", "eye", "eyebrow",
            "fabric", "face", "faculty", "fade", "faint", "faith", "fall", "false", "fame", "family",
            "famous", "fan", "fancy", "fantasy", "farm", "fashion", "fat", "fatal", "father", "fatigue",
            "fault", "favorite", "feature", "february", "federal", "fee", "feed", "feel", "female", "fence",
            "festival", "fetch", "fever", "few", "fiber", "fiction", "field", "figure", "file", "film",
            "filter", "final", "find", "fine", "finger", "finish", "fire", "firm", "first", "fiscal",
            "fish", "fit", "fitness", "fix", "flag", "flame", "flash", "flat", "flavor", "flee",
            "flight", "flip", "float", "flock", "floor", "flower", "fluid", "flush", "fly", "foam",
            "focus", "fog", "foil", "fold", "follow", "food", "foot", "force", "forest", "forget",
            "fork", "fortune", "forum", "forward", "fossil", "foster", "found", "fox", "fragile", "frame",
            "frequent", "fresh", "friend", "fringe", "frog", "front", "frost", "frown", "frozen", "fruit",
            "fuel", "fun", "funny", "furnace", "fury", "future", "gadget", "gain", "galaxy", "gallery",
            "game", "gap", "garage", "garbage", "garden", "garlic", "garment", "gas", "gasp", "gate",
            "gather", "gauge", "gaze", "general", "genius", "genre", "gentle", "genuine", "gesture", "ghost",
            "giant", "gift", "giggle", "ginger", "giraffe", "girl", "give", "glad", "glance", "glare",
            "glass", "glide", "glimpse", "globe", "gloom", "glory", "glove", "glow", "glue", "goat",
            "goddess", "gold", "good", "goose", "gorilla", "gospel", "gossip", "govern", "gown", "grab",
            "grace", "grain", "grant", "grape", "grass", "gravity", "great", "green", "grid", "grief",
            "grit", "grocery", "group", "grow", "grunt", "guard", "guess", "guide", "guilt", "guitar",
            "gun", "gym", "habit", "hair", "half", "hammer", "hamster", "hand", "happy", "harbor",
            "hard", "harsh", "harvest", "hat", "have", "hawk", "hazard", "head", "health", "heart",
            "heavy", "hedgehog", "height", "hello", "helmet", "help", "hen", "hero", "hidden", "high",
            "hill", "hint", "hip", "hire", "history", "hobby", "hockey", "hold", "hole", "holiday",
            "hollow", "home", "honey", "hood", "hope", "horn", "horror", "horse", "hospital", "host",
            "hotel", "hour", "hover", "hub", "huge", "human", "humble", "humor", "hundred", "hungry",
            "hunt", "hurdle", "hurry", "hurt", "husband", "hybrid", "ice", "icon", "idea", "identify",
            "idle", "ignore", "ill", "illegal", "illness", "image", "imitate", "immense", "immune", "impact",
            "impose", "improve", "impulse", "inch", "include", "income", "increase", "index", "indicate", "indoor",
            "industry", "infant", "inflict", "inform", "inhale", "inherit", "initial", "inject", "injury", "inmate",
            "inner", "innocent", "input", "inquiry", "insane", "insect", "inside", "inspire", "install", "intact",
            "interest", "into", "invest", "invite", "involve", "iron", "island", "isolate", "issue", "item",
            "ivory", "jacket", "jaguar", "jar", "jazz", "jealous", "jeans", "jelly", "jewel", "job",
            "join", "joke", "journey", "joy", "judge", "juice", "jump", "jungle", "junior", "junk",
            "just", "kangaroo", "keen", "keep", "ketchup", "key", "kick", "kid", "kidney", "kind",
            "kingdom", "kiss", "kit", "kitchen", "kite", "kitten", "kiwi", "knee", "knife", "knock",
            "know", "lab", "label", "labor", "ladder", "lady", "lake", "lamp", "language", "laptop",
            "large", "later", "latin", "laugh", "laundry", "lava", "law", "lawn", "lawsuit", "layer",
            "lazy", "leader", "leaf", "learn", "leave", "lecture", "left", "leg", "legal", "legend",
            "leisure", "lemon", "lend", "length", "lens", "leopard", "lesson", "letter", "level", "liar",
            "liberty", "library", "license", "life", "lift", "light", "like", "limb", "limit", "link",
            "lion", "liquid", "list", "little", "live", "lizard", "load", "loan", "lobster", "local",
            "lock", "logic", "lonely", "long", "loop", "lottery", "loud", "lounge", "love", "loyal",
            "lucky", "luggage", "lumber", "lunar", "lunch", "luxury", "lyrics", "machine", "mad", "magic",
            "magnet", "maid", "mail", "main", "major", "make", "mammal", "man", "manage", "mandate",
            "mango", "mansion", "manual", "maple", "marble", "march", "margin", "marine", "market", "marriage",
            "mask", "mass", "master", "match", "material", "math", "matrix", "matter", "maximum", "maze",
            "meadow", "mean", "measure", "meat", "mechanic", "medal", "media", "melody", "melt", "member",
            "memory", "mention", "menu", "mercy", "merge", "merit", "merry", "mesh", "message", "metal",
            "method", "middle", "midnight", "milk", "million", "mimic", "mind", "minimum", "minor", "minute",
            "miracle", "mirror", "misery", "miss", "mistake", "mix", "mixed", "mixture", "mobile", "model",
            "modify", "mom", "moment", "monitor", "monkey", "monster", "month", "moon", "moral", "more",
            "morning", "mosquito", "mother", "motion", "motor", "mountain", "mouse", "move", "movie", "much",
            "muffin", "mule", "multiply", "muscle", "museum", "music", "must", "mutual", "myself", "mystery",
            "myth", "naive", "name", "napkin", "narrow", "nasty", "nation", "nature", "near", "neck",
            "need", "negative", "neglect", "neither", "nephew", "nerve", "nest", "net", "network", "neutral",
            "never", "news", "next", "nice", "night", "noble", "noise", "nominee", "noodle", "normal",
            "north", "nose", "notable", "note", "nothing", "notice", "novel", "now", "nuclear", "number",
            "nurse", "nut", "oak", "obey", "object", "oblige", "obscure", "observe", "obtain", "obvious",
            "occur", "ocean", "october", "odor", "off", "offer", "office", "often", "oil", "okay",
            "old", "olive", "olympic", "omit", "once", "one", "onion", "online", "only", "open",
            "opera", "opinion", "oppose", "option", "orange", "orbit", "orchard", "order", "ordinary", "organ",
            "orient", "original", "orphan", "ostrich", "other", "outdoor", "outer", "output", "outrage", "outset",
            "outside", "oval", "oven", "over", "own", "owner", "oxygen", "oyster", "ozone", "pact",
            "paddle", "page", "pair", "palace", "palm", "panda", "panel", "panic", "panther", "paper",
            "parade", "parent", "park", "parrot", "party", "pass", "patch", "path", "patient", "patrol",
            "pattern", "pause", "pave", "payment", "peace", "peanut", "pear", "peasant", "pelican", "pen",
            "penalty", "pencil", "people", "pepper", "perfect", "permit", "person", "pet", "phone", "photo",
            "phrase", "physical", "piano", "picnic", "picture", "piece", "pig", "pigeon", "pill", "pilot",
            "pink", "pioneer", "pipe", "pistol", "pitch", "pizza", "place", "planet", "plastic", "plate",
            "play", "please", "pledge", "pluck", "plug", "plunge", "poem", "poet", "point", "polar",
            "pole", "police", "pond", "pony", "pool", "poor", "pop", "popcorn", "population", "porch",
            "port", "portion", "position", "possible", "post", "potato", "pottery", "poverty", "powder", "power",
            "practice", "praise", "predict", "prefer", "prepare", "present", "pretty", "prevent", "price", "pride",
            "primary", "print", "priority", "prison", "private", "prize", "problem", "process", "produce", "profit",
            "program", "project", "promote", "proof", "property", "prosper", "protect", "proud", "provide", "public",
            "pudding", "pull", "pulp", "pulse", "pumpkin", "punch", "pupil", "puppy", "purchase", "purity",
            "purpose", "purse", "push", "put", "puzzle", "pyramid", "quality", "quantum", "quarter", "question",
            "quick", "quit", "quiz", "quote", "rabbit", "raccoon", "race", "rack", "radar", "radio",
            "rail", "rain", "raise", "rally", "ramp", "ranch", "random", "range", "rapid", "rare",
            "rate", "rather", "raven", "raw", "razor", "ready", "real", "reason", "rebel", "rebuild",
            "recall", "receive", "recipe", "record", "recycle", "reduce", "reflect", "reform", "refuse", "region",
            "regret", "regular", "reject", "relax", "release", "relief", "rely", "remain", "remember", "remind",
            "remove", "render", "renew", "rent", "reopen", "repair", "repeat", "replace", "report", "require",
            "rescue", "resemble", "resist", "resource", "response", "result", "retire", "retreat", "return", "reunion",
            "reveal", "review", "reward", "rhythm", "rib", "ribbon", "rice", "rich", "ride", "ridge",
            "rifle", "right", "rigid", "ring", "riot", "ripple", "risk", "ritual", "rival", "river",
            "road", "roast", "robot", "robust", "rocket", "romance", "roof", "rookie", "room", "rose",
            "rotate", "rough", "round", "route", "royal", "rubber", "rude", "rug", "rule", "run",
            "runway", "rural", "sad", "saddle", "sadness", "safe", "sail", "salad", "salmon", "salon",
            "salt", "salute", "same", "sample", "sand", "satisfy", "satoshi", "sauce", "sausage", "save",
            "say", "scale", "scan", "scare", "scatter", "scene", "scheme", "school", "science", "scissors",
            "scorpion", "scout", "scrap", "screen", "script", "scrub", "sea", "search", "season", "seat",
            "second", "secret", "section", "secure", "seed", "seek", "segment", "select", "sell", "seminar",
            "senior", "sense", "sentence", "series", "service", "session", "settle", "setup", "seven", "shadow",
            "shaft", "shallow", "share", "shed", "shell", "sheriff", "shield", "shift", "shine", "ship",
            "shiver", "shock", "shoe", "shoot", "shop", "short", "shoulder", "shove", "shrimp", "shrug",
            "shuffle", "shy", "sibling", "sick", "side", "siege", "sight", "sign", "silent", "silk",
            "silly", "silver", "similar", "simple", "since", "sing", "siren", "sister", "situate", "six",
            "size", "skate", "sketch", "ski", "skill", "skin", "skirt", "skull", "slab", "slam",
            "sleep", "slender", "slice", "slide", "slight", "slim", "slogan", "slot", "slow", "slush",
            "small", "smart", "smile", "smoke", "smooth", "snack", "snake", "snap", "sniff", "snow",
            "soap", "soccer", "social", "sock", "soda", "soft", "solar", "soldier", "solid", "solution",
            "solve", "someone", "song", "soon", "sorry", "sort", "soul", "sound", "soup", "source",
            "south", "space", "spare", "spatial", "spawn", "speak", "special", "speed", "spell", "spend",
            "sphere", "spice", "spider", "spike", "spin", "spirit", "split", "spoil", "sponsor", "spoon",
            "sport", "spot", "spray", "spread", "spring", "spy", "square", "squeeze", "squirrel", "stable",
            "stadium", "staff", "stage", "stairs", "stamp", "stand", "start", "state", "stay", "steak",
            "steel", "stem", "step", "stereo", "stick", "still", "sting", "stock", "stomach", "stone",
            "stool", "story", "stove", "strategy", "street", "strike", "strong", "struggle", "student", "stuff",
            "stumble", "style", "subject", "submit", "subway", "success", "such", "sudden", "suffer", "sugar",
            "suggest", "suit", "summer", "sun", "sunny", "sunset", "super", "supply", "supreme", "sure",
            "surface", "surge", "surprise", "surround", "survey", "suspect", "sustain", "swallow", "swamp", "swap",
            "swarm", "swear", "sweet", "swift", "swim", "swing", "switch", "sword", "symbol", "symptom",
            "syrup", "system", "table", "tackle", "tag", "tail", "talent", "talk", "tank", "tape",
            "target", "task", "taste", "tattoo", "taxi", "teach", "team", "tell", "ten", "tenant",
            "tennis", "tent", "term", "test", "text", "thank", "that", "theme", "then", "theory",
            "there", "they", "thing", "this", "thought", "three", "thrive", "throw", "thumb", "thunder",
            "ticket", "tide", "tiger", "tilt", "timber", "time", "tiny", "tip", "tired", "tissue",
            "title", "toast", "tobacco", "today", "toddler", "toe", "together", "toilet", "token", "tomato",
            "tomorrow", "tone", "tongue", "tonight", "tool", "tooth", "top", "topic", "topple", "torch",
            "tornado", "tortoise", "toss", "total", "tourist", "toward", "tower", "town", "toy", "track",
            "trade", "traffic", "tragic", "train", "transfer", "trap", "trash", "travel", "tray", "treat",
            "tree", "trend", "trial", "tribe", "trick", "trigger", "trim", "trip", "trophy", "trouble",
            "truck", "true", "truly", "trumpet", "trust", "truth", "try", "tube", "tuition", "tumble",
            "tuna", "tunnel", "turkey", "turn", "turtle", "twelve", "twenty", "twice", "twin", "twist",
            "two", "type", "typical", "ugly", "umbrella", "unable", "unaware", "uncle", "uncover", "under",
            "undo", "unfair", "unfold", "unhappy", "uniform", "unique", "unit", "universe", "unknown", "unlock",
            "until", "unusual", "unveil", "update", "upgrade", "uphold", "upon", "upper", "upset", "urban",
            "urge", "usage", "use", "used", "useful", "useless", "usual", "utility", "vacant", "vacuum",
            "vague", "valid", "valley", "valve", "van", "vanish", "vapor", "various", "vast", "vault",
            "vehicle", "velvet", "vendor", "venture", "venue", "verb", "verify", "version", "very", "vessel",
            "veteran", "viable", "vibrant", "vicious", "victory", "video", "view", "village", "vintage", "violin",
            "virtual", "virus", "visa", "visit", "visual", "vital", "vivid", "vocal", "voice", "void",
            "volcano", "volume", "vote", "voyage", "wage", "wagon", "wait", "walk", "wall", "walnut",
            "want", "warfare", "warm", "warrior", "wash", "wasp", "waste", "water", "wave", "way",
            "wealth", "weapon", "wear", "weasel", "weather", "web", "wedding", "weekend", "weird", "welcome",
            "west", "wet", "whale", "what", "wheat", "wheel", "when", "where", "whip", "whisper",
            "wide", "width", "wife", "wild", "will", "win", "window", "wine", "wing", "wink",
            "winner", "winter", "wire", "wisdom", "wise", "wish", "witness", "wolf", "woman", "wonder",
            "wood", "wool", "word", "work", "world", "worry", "worth", "wrap", "wreck", "wrestle",
            "wrist", "write", "wrong", "yard", "year", "yellow", "you", "young", "youth", "zebra",
            "zero", "zone", "zoo"
        };

        public sealed class Options
        {
            public int WordCount { get; set; } = DefaultWordCount;
            public bool Capitalize { get; set; } = true;
            public bool IncludeNumbers { get; set; } = true;
            public bool IncludeSpecialCharacters { get; set; } = true;
        }

        public static string Generate(Options options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            int count = Math.Clamp(options.WordCount, MinWordCount, MaxWordCount);
            var parts = new string[count];

            for (int i = 0; i < count; i++)
            {
                string word = Words[RandomNumberGenerator.GetInt32(Words.Length)];
                if (options.Capitalize && word.Length > 0)
                {
                    word = char.ToUpper(word[0], CultureInfo.InvariantCulture) + word.Substring(1);
                }

                if (options.IncludeNumbers)
                {
                    word += RandomNumberGenerator.GetInt32(0, 10).ToString(CultureInfo.InvariantCulture);
                }

                parts[i] = word;
            }

            string separator = options.IncludeSpecialCharacters ? "-" : " ";
            return string.Join(separator, parts);
        }
    }
}