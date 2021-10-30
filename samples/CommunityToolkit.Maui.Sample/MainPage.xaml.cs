﻿using CommunityToolkit.Maui.Sample.Pages;
using Microsoft.Maui;

namespace CommunityToolkit.Maui.Sample;

public partial class MainPage : BasePage
{
	public MainPage()
	{
		InitializeComponent();

		Page ??= this;

		Padding = new Thickness(20, 0);
	}
}