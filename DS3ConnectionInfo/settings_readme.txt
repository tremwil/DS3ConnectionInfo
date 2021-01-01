"overlay" (boolean): Turns the in-game overlay on (true) or off (false). Note that
the overlay only works in windowed mode.

"showRegion" (boolean): If true, will also display region information on the 
overlay as is done in the console. This is false by default to allow for 
gameplay footage to be shared without the risk of spreading private information.

"xOffset" (number): The horizontal offset of the end of the text from the right 
side of the game window, as a fraction of the window's width. For example, 
a value of 0.1 with a window width of 2000 pixels would fix the right side 
of the textbox at 200 pixels from the right of the window. Default value is
0.025.

"yOffset" (number): The vertical offset of the first line of text from the top 
of the game window, as a fraction of the window's height. For example, a value 
of 0.1 with a window height of 1000 pixels would fix the first line of text
100 pixels from the top of the window. Default value is 0.05.

"textScale" (number): Controls the font size of the text in the overlay as a 
multiple of the default font size. The default font size scales dynamically 
with the size of the window, so you may want to increase it if playing the game
in a smaller window. Default value is 1.0.