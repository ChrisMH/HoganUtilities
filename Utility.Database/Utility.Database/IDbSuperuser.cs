﻿namespace Utility.Database
{
    public interface IDbSuperuser
    {
        string UserName { get; set; }
        string Password { get; set; }
    }
}