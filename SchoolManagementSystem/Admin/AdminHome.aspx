<%@ Page Title="" Language="C#" MasterPageFile="../MasterPage/AdminMst.Master" AutoEventWireup="true" CodeBehind="AdminHome.aspx.cs" Inherits="SchoolManagementSystem.Admin.AdminHome" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <div style="background-image: url('../CssContent/Images/logo.png'); width: 100%; height: 920px; background-repeat: no-repeat; background-size: 600px 600px; background-position: center; padding: 0px;">
    <div class="container p-md-4 p-sm-4">
        <div>
            <asp:Label ID="lblMsg" runat="server"></asp:Label>
        </div>
      
           <h2 class="text-center" style="text-align: center; 
            color:lightskyblue; font-size:30px; font-weight:bold;
            height:40px; text-align:center;
                box-shadow: 0 3px 5px rgba(0, 0, 0, 0.125);">Admin Home Page</h2>

    </div>
</div>

</asp:Content>
