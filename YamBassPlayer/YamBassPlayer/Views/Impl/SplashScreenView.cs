using Terminal.Gui;

namespace YamBassPlayer.Views.Impl;

internal class SplashScreenView : Window
{
    public SplashScreenView()
    {
        var art = @"
                                                                          .-~~~-.                                       
                   .-~~~-.                                        .- ~ ~-(       )_ _                                   
           .- ~ ~-(       )_ _                                   /                     ~ -.                             
          /                    ~ -.                             |                           \              '            
         |                          ',                           \                         .'            \  ,  /        
          \                         .' -~~~-.                      ~- . _____________ . -~           ' ,___/_\___, '    
            ~- ._ ,. ,.,.,., ,.. -~~-(       )_ _                                                       \ /o.o\ /       
                             /                    ~ -.                                              -=   > \_/ <   =-   
                            |                          ',                                               /_\___/_\       
                             \                         .'                                            . `   \ /   ` .    
                               ~- ._ ,. ,.,.,., ,.. -~                                                   /  `  \        
                                                                                                                        
            .'\   /`.                                                                                                   
          .'.-.`-'.-.`.            __        __   _ _                            _____                                  
     ..._:   .-. .-.   :_...       \ \      / /__| | | ___ ___  _ __ ___   ___  |_   _|__                               
   .'    '-.( o) ( o).-'    `.      \ \ /\ / / _ \ | |/ __/ _ \| '_ ` _ \ / _ \   | |/ _ \                              
  :  _    _ _`~(_)~`_ _    _  :      \ V  V /  __/ | | (_| (_) | | | | | |  __/   | | (_) |                             
 :  /:   ' .-=_   _=-. `   ;\  :   __ \_/\_/ \___|_|_|\___\___/|_| |_| |_|\___|_  |_|\___/                              
 :   :|-.._  '     `  _..-|:   :   \ \ / /_ _ _ __ ___ | __ )  __ _ ___ ___|  _ \| | __ _ _   _  ___ _ __               
  :   `:| |`:-:-.-:-:'| |:'   :     \ V / _` | '_ ` _ \|  _ \ / _` / __/ __| |_) | |/ _` | | | |/ _ \ '__|              
   `.   `.| | | | | | |.'   .'       | | (_| | | | | | | |_) | (_| \__ \__ \  __/| | (_| | |_| |  __/ |                 
     `.   `-:_| | |_:-'   .'         |_|\__,_|_| |_| |_|____/ \__,_|___/___/_|   |_|\__,_|\__, |\___|_|                 
       `-._   ````    _.-'                                                                |___/                         
                                                                                                                        
                        ";

        View artView = CreateCenteredAsciiArt(art);
        Add(artView);

    }

    public sealed override void Add(View view)
    {
        base.Add(view);
    }


    public static View CreateCenteredAsciiArt(string art)
    {
        var trimmed = art.Trim('\r', '\n');

        var lines = trimmed.Split('\n');
        int artWidth = lines.Max(l => l.Replace("\r", "").Length);
        int artHeight = lines.Length;

        var label = new Label(trimmed)
        {
            X = Pos.Center(),
            Y = Pos.Center(),
            Width = artWidth,
            Height = artHeight,
            TextAlignment = TextAlignment.Left,
            VerticalTextAlignment = VerticalTextAlignment.Top,
            AutoSize = false,
        };

        return label;
    }
}