# SolutionStructure.cmd
@echo off
PowerShell -NoProfile -ExecutionPolicy Bypass -File "%~dp0SolutionStructure.ps1"

PowerShell -NoProfile -ExecutionPolicy Bypass -File "%~dp0ExtractSolutionCode.ps1"
pause