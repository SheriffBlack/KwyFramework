using System;
using KwyTemplate.Security.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace KwyTemplate.Security.Migrations;

[DbContext(typeof(SecurityDbContext))]
partial class SecurityDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder.HasAnnotation("ProductVersion", "8.0.28");

        modelBuilder.Entity("KwyTemplate.Security.Data.LocalUser", b =>
        {
            b.Property<long>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");

            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.Property<string>("DisplayName")
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnType("TEXT");

            b.Property<bool>("IsEnabled")
                .HasColumnType("INTEGER");

            b.Property<int>("Level")
                .HasColumnType("INTEGER");

            b.Property<string>("PasswordHash")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<string>("PasswordSalt")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<string>("UserName")
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnType("TEXT")
                .UseCollation("NOCASE");

            b.HasKey("Id");

            b.HasIndex("UserName")
                .IsUnique();

            b.ToTable("Users");
        });
#pragma warning restore 612, 618
    }
}