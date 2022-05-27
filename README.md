# What is Benford’s law?

We all know about the [Normal distribution](https://en.wikipedia.org/wiki/Normal_distribution) and its ubiquity in all kind of natural phenomena or observations. But there is another law of numbers which does not get much attention but pops up everywhere — from nations’ population to stock market volumes to the domain of universal physical constants.

**Benford’s Law**, also known as the **Law of First Digits**, the **Phenomenon of Significant Digits**, or the **Law of Anomalous Numbers** is the finding that the first digits (or numerals to be exact) of the numbers found in series of records of the most varied sources do not display a uniform distribution, but rather are arranged in such a way that the digit “1” is the most frequent, followed by “2”, “3”, and so in a successively decreasing manner down to “9”.

The distribution of first digits should follow the pattern in the image below:

![Benford's Law Chart](https://upload.wikimedia.org/wikipedia/commons/thumb/4/46/Rozklad_benforda.svg/440px-Rozklad_benforda.svg.png)

This distribution is specifically:

```
Digit   Benford [%]
=====   ===========
1       30.10
2       17.61
3       12.49
4       09.69
5       07.92
6       06.69
7       05.80
8       05.12
9       04.58
```

## Project

This project is a .NET 5.0 console app project to play with Benford's Law on images and a text file with numeric values. You can fill the text file with a large, random data set from most any source to see if the data follows this law and if not, implying the data has been artificially altered. Likewise you can put a set of images (*.jpg, *.tiff) into the *images* folder to analyze the pixel values in them. If the image results deviate from the law it could mean the image has been processed or altered.
