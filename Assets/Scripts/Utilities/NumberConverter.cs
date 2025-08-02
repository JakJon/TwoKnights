using UnityEngine;

public static class NumberConverter
{
    /// <summary>
    /// Converts an integer to Roman numeral representation.
    /// Supports numbers from 1 to 3999.
    /// </summary>
    /// <param name="number">The number to convert</param>
    /// <returns>Roman numeral string, or regular number string if out of range</returns>
    public static string ToRoman(int number)
    {
        if (number <= 0 || number > 3999)
            return number.ToString(); // Fallback to regular number if out of Roman numeral range

        var romanNumerals = new (int value, string numeral)[]
        {
            (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"),
            (100, "C"), (90, "XC"), (50, "L"), (40, "XL"),
            (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
        };

        string result = "";
        
        foreach (var (value, numeral) in romanNumerals)
        {
            while (number >= value)
            {
                result += numeral;
                number -= value;
            }
        }
        
        return result;
    }

    /// <summary>
    /// Converts a number to written words (basic implementation for numbers 0-99).
    /// Can be extended for larger numbers if needed.
    /// </summary>
    /// <param name="number">The number to convert</param>
    /// <returns>Written word representation of the number</returns>
    public static string ToWords(int number)
    {
        if (number == 0) return "zero";
        if (number < 0) return "negative " + ToWords(-number);

        string[] ones = 
        {
            "", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
            "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", 
            "seventeen", "eighteen", "nineteen"
        };

        string[] tens = 
        {
            "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"
        };

        string result = "";

        if (number >= 100)
        {
            result += ones[number / 100] + " hundred ";
            number %= 100;
        }

        if (number >= 20)
        {
            result += tens[number / 10];
            if (number % 10 > 0)
                result += "-" + ones[number % 10];
        }
        else if (number > 0)
        {
            result += ones[number];
        }

        return result.Trim();
    }
}
