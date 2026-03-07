using Core.Infrastructure.Views;
using Features.Login.Events;
using Features.Login.Infrastructure.Attributes;
using Features.Login.Requests;
using UnityEngine;
using UnityEngine.UIElements;

namespace Features.Login.View
{
	/// <summary>
	/// View for Login. Manages UI Toolkit elements for the login screen.
	/// Requires a UIDocument component on the same GameObject.
	/// </summary>
	public sealed class LoginView : BaseView
	{
		[Header("Asset References")]
		[SerializeField] private Texture2D _titleImage;
		[SerializeField] private Texture2D _characterImage;

		private UIDocument _uiDocument;
		private TextField _usernameField;
		private TextField _passwordField;
		private Button _loginButton;
		private Label _forgotPassword;
		private Button _googleButton;
		private Button _facebookButton;
		private Button _appleButton;
		private Label _registerLink;
		private VisualElement _passwordToggle;
		private Label _passwordToggleIcon;
		private bool _passwordVisible;

		/// <summary>
		/// Queries all UI Toolkit elements from the UIDocument.
		/// </summary>
		protected override void Awake()
		{
			base.Awake();
			_uiDocument = GetComponent<UIDocument>();
		}

		/// <summary>
		/// Binds UI callbacks and sets asset images after scope is active.
		/// </summary>
		protected override void OnEnabled()
		{
			var root = _uiDocument.rootVisualElement;

			// Query elements
			_usernameField = root.Q<TextField>("username-field");
			_passwordField = root.Q<TextField>("password-field");
			_loginButton = root.Q<Button>("login-button");
			_forgotPassword = root.Q<Label>("forgot-password");
			_googleButton = root.Q<Button>("google-button");
			_facebookButton = root.Q<Button>("facebook-button");
			_appleButton = root.Q<Button>("apple-button");
			_registerLink = root.Q<Label>("register-link");
			_passwordToggle = root.Q<VisualElement>("password-toggle");
			_passwordToggleIcon = root.Q<Label>("password-toggle-icon");

			// Set asset images from serialized references
			SetAssetImages(root);

			// Set placeholders
			_usernameField.textEdition.placeholder = "Cậu là...";
			_passwordField.textEdition.placeholder = "********";

			// Register callbacks
			_loginButton.clicked += OnLoginClicked;
			_forgotPassword.RegisterCallback<ClickEvent>(OnForgotPasswordClicked);
			_googleButton.clicked += OnGoogleLoginClicked;
			_facebookButton.clicked += OnFacebookLoginClicked;
			_appleButton.clicked += OnAppleLoginClicked;
			_registerLink.RegisterCallback<ClickEvent>(OnRegisterClicked);
			_passwordToggle.RegisterCallback<ClickEvent>(OnPasswordToggleClicked);
		}

		/// <summary>
		/// Unregisters UI callbacks when view is disabled.
		/// </summary>
		protected override void OnDisabled()
		{
			if (_loginButton != null) _loginButton.clicked -= OnLoginClicked;
			if (_forgotPassword != null) _forgotPassword.UnregisterCallback<ClickEvent>(OnForgotPasswordClicked);
			if (_googleButton != null) _googleButton.clicked -= OnGoogleLoginClicked;
			if (_facebookButton != null) _facebookButton.clicked -= OnFacebookLoginClicked;
			if (_appleButton != null) _appleButton.clicked -= OnAppleLoginClicked;
			if (_registerLink != null) _registerLink.UnregisterCallback<ClickEvent>(OnRegisterClicked);
			if (_passwordToggle != null) _passwordToggle.UnregisterCallback<ClickEvent>(OnPasswordToggleClicked);
		}

		/// <summary>
		/// Sets the title and character background images from serialized Texture2D references.
		/// </summary>
		/// <param name="root">Root visual element of the UIDocument.</param>
		private void SetAssetImages(VisualElement root)
		{
			if (_titleImage != null)
			{
				var titleElement = root.Q<VisualElement>("title-image");
				if (titleElement != null)
				{
					titleElement.style.backgroundImage = new StyleBackground(_titleImage);
				}
			}

			if (_characterImage != null)
			{
				var characterElement = root.Q<VisualElement>("character-image");
				if (characterElement != null)
				{
					characterElement.style.backgroundImage = new StyleBackground(_characterImage);
				}
			}
		}

		/// <summary>
		/// Handles login button click. Sends login request with username and password.
		/// </summary>
		private void OnLoginClicked()
		{
			string username = _usernameField.value;
			string password = _passwordField.value;
			SendRequest(LoginRequests.Login, new LoginPayload(username, password));
		}

		/// <summary>
		/// Toggles password field visibility.
		/// </summary>
		private void OnPasswordToggleClicked(ClickEvent evt)
		{
			_passwordVisible = !_passwordVisible;
			_passwordField.isPasswordField = !_passwordVisible;
			_passwordToggleIcon.text = _passwordVisible ? "\u1F441" : "\u1F441\u0338";
		}

		private void OnForgotPasswordClicked(ClickEvent evt)
		{
			SendRequest(LoginRequests.ForgotPassword);
		}

		private void OnGoogleLoginClicked()
		{
			SendRequest(LoginRequests.SocialLogin, "google");
		}

		private void OnFacebookLoginClicked()
		{
			SendRequest(LoginRequests.SocialLogin, "facebook");
		}

		private void OnAppleLoginClicked()
		{
			SendRequest(LoginRequests.SocialLogin, "apple");
		}

		private void OnRegisterClicked(ClickEvent evt)
		{
			SendRequest(LoginRequests.Register);
		}

		/// <summary>
		/// Handles successful login event from controller.
		/// </summary>
		/// <param name="payload">Success payload from controller.</param>
		[OnEvent(LoginEvents.LoginSuccess)]
		private void OnLoginSuccess(object payload)
		{
			Debug.Log("[LoginView] Login successful: " + payload, this);
		}

		/// <summary>
		/// Handles failed login event from controller.
		/// </summary>
		/// <param name="payload">Error message from controller.</param>
		[OnEvent(LoginEvents.LoginFailed)]
		private void OnLoginFailed(object payload)
		{
			Debug.LogWarning("[LoginView] Login failed: " + payload, this);
		}
	}

	/// <summary>
	/// Payload for login request containing username and password.
	/// </summary>
	public sealed class LoginPayload
	{
		/// <summary>Username or email entered by the user.</summary>
		public string Username { get; }

		/// <summary>Password entered by the user.</summary>
		public string Password { get; }

		/// <summary>
		/// Creates a new LoginPayload.
		/// </summary>
		/// <param name="username">Username or email.</param>
		/// <param name="password">Password.</param>
		public LoginPayload(string username, string password)
		{
			Username = username;
			Password = password;
		}
	}
}
