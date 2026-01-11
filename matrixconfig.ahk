/*
AHK script to help debugging matrix configuration settings
Quickly fills in specific values for testing purposes.
*/
#Requires AutoHotkey v2.0

F3::
{
    ; Cols
    Send("{tab 6}")
    Send("128")

    ; GPIO slowdown
    Send("{tab 4}")
    Send("2")

    ; Parallel
    Send("{tab 14}")
    Send("2")

    ; Row Address Type
    Send("{tab 10}")
    Send("5")

    ; Rows
    Send("{tab 2}")
    Send("64")
    Return
}