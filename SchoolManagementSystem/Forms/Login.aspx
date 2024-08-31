<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="SchoolManagementSystem.Forms.Login" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Login - School Management System</title>
    <link href="../CssContent/Content/styles.css" rel="stylesheet" />
    <style>
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            margin: 0;
            padding: 0;
            background: url('../CssContent/Images/school-background1.jpg') no-repeat center center fixed;
            background-size: cover;
            height: 100vh;
            display: flex;
            justify-content: center;
            align-items: center;
        }

        .overlay {
            background-color: rgba(0, 0, 0, 0.6);
            position: absolute;
            top: 0;
            bottom: 0;
            left: 0;
            right: 0;
        }

        .login-container {
            background-color: rgba(255, 255, 255, 0.0);
            border-radius: 10px;
            padding: 30px;
            width: 100%;
            box-shadow: 0px 0px 20px rgba(0, 0, 0, 0.5);
            position: relative;
            z-index: 2;
        }

        .login-header {
            text-align: center;
            margin-bottom: 30px;
            color: #fff;
        }

            .login-header h2 {
                margin: 0;
                font-size: 28px;
                font-weight: bold;
            }

            .login-header p {
                font-size: 16px;
                color: #f8f9fa;
            }

        .form-group {
            margin-bottom: 20px;
        }

            .form-group label {
                display: block;
                color: #f8f9fa;
                margin-bottom: 5px;
                font-size: 14px;
            }

            .form-group input[type="text"],
            .form-group input[type="password"] {
                width: 100%;
                padding: 12px;
                border: 1px solid #ddd;
                border-radius: 5px;
                font-size: 14px;
                box-sizing: border-box;
            }

            .form-group input[type="checkbox"] {
                margin-right: 5px;
            }

            .form-group button {
                width: 100%;
                padding: 12px;
                background-color: #28a745;
                border: none;
                border-radius: 5px;
                color: #fff;
                font-size: 16px;
                cursor: pointer;
                transition: background-color 0.3s;
            }

                .form-group button:hover {
                    background-color: #218838;
                }

        .login-footer {
            text-align: center;
            margin-top: 20px;
            margin-bottom: 20px;
        }

            .login-footer a {
                color: #28a745;
                text-decoration: none;
                font-size: 14px;
            }

                .login-footer a:hover {
                    text-decoration: underline;
                }

        .btn-login {
            width: 100%;
            display: inline-block;
            font-weight: 400;
            color: #212529;
            text-align: center;
            vertical-align: middle;
            -webkit-user-select: none;
            -moz-user-select: none;
            -ms-user-select: none;
            user-select: none;
            border: 1px solid transparent;
            padding: .375rem .75rem;
            font-size: 1rem;
            line-height: 1.5;
            border-radius: .25rem;
            transition: color .15s ease-in-out, background-color .15s ease-in-out, border-color .15s ease-in-out, box-shadow .15s ease-in-out;
        }

            .btn-login p {
                color: #fff;
                background-color: #007bff;
                border-color: #007bff;
            }

            .btn-login:hover {
                background-color: #a7a6a6;
            }
    </style>
</head>
<body>
    <div class="overlay"></div>
    <form id="form1" runat="server">
        <div class="login-container">
            <div class="login-header">
                <h2>School Management System</h2>
                <p>Login to your account</p>
            </div>
            <div class="form-group">
                <label for="username">Username:</label>
                <asp:TextBox ID="txtUsername" runat="server" CssClass="form-control" placeholder="Enter username" AutoCompleteType="Disabled"></asp:TextBox>
            </div>
            <div class="form-group">
                <label for="password">Password:</label>
                <asp:TextBox ID="txtPassword" runat="server" CssClass="form-control" TextMode="Password" placeholder="Enter password" AutoCompleteType="Disabled"></asp:TextBox>
            </div>
            <div class="login-footer">
                <a href="#">Forgot your password?</a>
            </div>
            <div class="form-group">
                <asp:Button ID="btnLogin" runat="server" Text="Login" CssClass="btn-login p" OnClick="btnLogin_Click" />
            </div>

        </div>
    </form>
</body>
</html>
